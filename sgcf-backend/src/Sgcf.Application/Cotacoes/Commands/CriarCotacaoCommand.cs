using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Application.Cotacoes.Services;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cria uma nova cotação em estado Rascunho.
/// Se CodigoInterno for nulo, gera automaticamente via repositório.
/// S40: prazo como tenor {valor, unidade}; moedaAlvo com PTAX multimoeda; prazoMaximoDias/moeda legados.
/// Onda 1: <see cref="ContratoMaeId"/> obrigatório para Refinimp (moeda herdada do contrato mãe). SPEC §4.1.
/// </summary>
public sealed record CriarCotacaoCommand(
    string Modalidade,
    decimal ValorAlvoBrl,
    DateOnly DataAbertura,
    string? CodigoInterno = null,
    int? PrazoMaximoDias = null,
    int? PrazoMaximoValor = null,
    string? PrazoMaximoUnidade = null,
    string? MoedaAlvo = null,
    string? Observacoes = null,
    Guid? ContratoMaeId = null) : IRequest<CotacaoDto>;

public sealed class CriarCotacaoCommandValidator : AbstractValidator<CriarCotacaoCommand>
{
    private static readonly ModalidadeContrato[] BrlPuras =
        [ModalidadeContrato.Nce, ModalidadeContrato.CapitalDeGiro, ModalidadeContrato.Fgi];

    public CriarCotacaoCommandValidator()
    {
        RuleFor(c => c.Modalidade)
            .NotEmpty()
            .Must(v => Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");

        RuleFor(c => c.ValorAlvoBrl)
            .GreaterThan(0m)
            .WithMessage("ValorAlvoBrl deve ser maior que zero.");

        // S40 §4.1: prazo obrigatório na criação (tenor estruturado OU dias legado).
        RuleFor(c => c)
            .Must(c => c.PrazoMaximoValor.HasValue || c.PrazoMaximoDias.HasValue)
            .WithMessage("Informe prazoMaximoValor (com unidade) ou prazoMaximoDias.");

        // S40 §4.3: validação dura do tenor.
        When(c => c.PrazoMaximoValor.HasValue, () =>
            RuleFor(c => c.PrazoMaximoValor!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PrazoMaximoValor deve ser maior ou igual a 1."));

        When(c => c.PrazoMaximoDias.HasValue, () =>
            RuleFor(c => c.PrazoMaximoDias!.Value)
                .GreaterThanOrEqualTo(1)
                .WithMessage("PrazoMaximoDias deve ser maior ou igual a 1."));

        When(c => c.PrazoMaximoUnidade is not null, () =>
            RuleFor(c => c.PrazoMaximoUnidade!)
                .Must(u => Enum.TryParse<UnidadePrazo>(u, true, out _))
                .WithMessage($"PrazoMaximoUnidade deve ser um dos valores: {string.Join(", ", Enum.GetNames<UnidadePrazo>())}."));

        // S40 §4.5: moedaAlvo deve pertencer ao enum quando informada.
        When(c => c.MoedaAlvo is not null, () =>
            RuleFor(c => c.MoedaAlvo!)
                .Must(m => Enum.TryParse<Moeda>(m, true, out _))
                .WithMessage($"MoedaAlvo deve ser um dos valores: {string.Join(", ", Enum.GetNames<Moeda>())}."));

        // S40 §4.5: modalidades BRL puras só aceitam moedaAlvo = Brl.
        When(c => c.MoedaAlvo is not null
                  && Enum.TryParse<ModalidadeContrato>(c.Modalidade, true, out var m)
                  && BrlPuras.Contains(m), () =>
            RuleFor(c => c.MoedaAlvo!)
                .Must(m => string.Equals(m, nameof(Moeda.Brl), StringComparison.OrdinalIgnoreCase))
                .WithMessage("moedaAlvo deve ser 'Brl' para as modalidades Nce, CapitalDeGiro e Fgi."));

        // Onda 1 — SPEC §5.1: ContratoMaeId obrigatório quando modalidade=Refinimp.
        RuleFor(c => c.ContratoMaeId)
            .NotNull().NotEqual(Guid.Empty)
            .When(c => Enum.TryParse<ModalidadeContrato>(c.Modalidade, true, out var m)
                        && m == ModalidadeContrato.Refinimp)
            .WithMessage("ContratoMaeId é obrigatório para a modalidade Refinimp.");

        // Defesa: outras modalidades não devem receber ContratoMaeId.
        RuleFor(c => c.ContratoMaeId)
            .Null()
            .When(c => Enum.TryParse<ModalidadeContrato>(c.Modalidade, true, out var m)
                        && m != ModalidadeContrato.Refinimp
                        && c.ContratoMaeId.HasValue)
            .WithMessage("ContratoMaeId não se aplica à modalidade informada.");
    }
}

public sealed class CriarCotacaoCommandHandler(
    ICotacaoRepository repo,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock,
    IContratoRepository? contratoRepo = null) : IRequestHandler<CriarCotacaoCommand, CotacaoDto>
{
    private static readonly DateTimeZone Brasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<CotacaoDto> Handle(CriarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        LocalDate dataAbertura = new(cmd.DataAbertura.Year, cmd.DataAbertura.Month, cmd.DataAbertura.Day);
        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);

        List<AlertaDto> alertas = [];

        // S40 §4.1: resolve o tenor (precedência valor+unidade > dias legado; default por modalidade).
        UnidadePrazo? unidade = cmd.PrazoMaximoUnidade is null
            ? null
            : Enum.Parse<UnidadePrazo>(cmd.PrazoMaximoUnidade, true);
        ResolvedorTenor.Resultado tenor = ResolvedorTenor.Resolver(
            modalidade, cmd.PrazoMaximoValor, unidade, cmd.PrazoMaximoDias);
        if (tenor.Alerta is not null)
        {
            alertas.Add(tenor.Alerta);
        }

        // S40 §2.2/§6: determina a moeda alvo por modalidade (Refinimp herda do contrato mãe).
        Moeda moedaAlvo = await ResolverMoedaAlvoAsync(cmd, modalidade, alertas, cancellationToken);

        // S40 §6: PTAX D-1 por moeda alvo (apenas modalidades cambiais).
        decimal? ptax = null;
        LocalDate? dataPtaxReferencia = null;
        if (Cotacao.ExigeMoedaEstrangeira(modalidade))
        {
            CotacaoFx cotacaoFx = await cotacaoResolver.ResolverFxAsync(
                moedaAlvo, TipoCotacao.PtaxD1, dataAbertura, cancellationToken)
                ?? throw new PtaxIndisponivelException(
                    moedaAlvo.ToString(),
                    ToDateOnly(dataAbertura.PlusDays(-1)),
                    $"PTAX D-1 de {moedaAlvo}/BRL não disponível para a data de referência informada. " +
                    $"Cadastre a cotação {moedaAlvo}/BRL antes de criar a cotação.");

            ptax = cotacaoFx.ValorVenda.Valor;
            dataPtaxReferencia = cotacaoFx.Momento.InZone(Brasilia).Date;
        }

        string codigoInterno = cmd.CodigoInterno is not null && !string.IsNullOrWhiteSpace(cmd.CodigoInterno)
            ? cmd.CodigoInterno
            : await repo.GerarProximoCodigoInternoAsync(dataAbertura.Year, cancellationToken);

        Money valorAlvo = new(cmd.ValorAlvoBrl, Moeda.Brl);

        Cotacao cotacao = Cotacao.CriarComTenor(
            codigoInterno,
            modalidade,
            valorAlvo,
            tenor.Valor,
            tenor.Unidade,
            dataAbertura,
            moedaAlvo,
            dataPtaxReferencia: dataPtaxReferencia,
            ptaxUsada: ptax,
            clock,
            dominio: null,
            observacoes: cmd.Observacoes,
            contratoMaeId: cmd.ContratoMaeId);

        repo.Add(cotacao);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao, alertas);
    }

    /// <summary>
    /// Regras de moeda alvo (SPEC S40 §2.2, §4.5): BRL puras → Brl; Refinimp → herdada do mãe (read-only);
    /// Finimp/Lei4131 → enviada pelo operador, com default Usd (retrocompatível) quando ausente.
    /// </summary>
    private async Task<Moeda> ResolverMoedaAlvoAsync(
        CriarCotacaoCommand cmd,
        ModalidadeContrato modalidade,
        List<AlertaDto> alertas,
        CancellationToken cancellationToken)
    {
        if (modalidade == ModalidadeContrato.Refinimp)
        {
            if (contratoRepo is null)
            {
                throw new InvalidOperationException(
                    "IContratoRepository é obrigatório para criar cotação da modalidade Refinimp.");
            }

            Contrato mae = await contratoRepo.GetByIdAsync(cmd.ContratoMaeId!.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Contrato mãe '{cmd.ContratoMaeId.Value}' não encontrado.");

            // Rejeita status finais ou inválidos para refinanciamento — SPEC §4.1 e §8.1.
            if (mae.Status is StatusContrato.Cancelado
                           or StatusContrato.Liquidado
                           or StatusContrato.RefinanciadoTotal)
            {
                throw new InvalidOperationException(
                    $"Contrato mãe '{cmd.ContratoMaeId.Value}' está em status '{mae.Status}' " +
                    "e não pode ser refinanciado.");
            }

            // Moeda herdada do mãe; valor enviado divergente é ignorado com alerta. SPEC S40 §4.5.
            if (cmd.MoedaAlvo is not null
                && !string.Equals(cmd.MoedaAlvo, mae.Moeda.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                alertas.Add(new AlertaDto(
                    "moeda-herdada-do-contrato-mae",
                    "moedaAlvo",
                    SeveridadeAlertaCotacao.Info,
                    $"moedaAlvo informada foi ignorada; Refinimp herda a moeda do contrato mãe ({mae.Moeda})."));
            }

            return mae.Moeda;
        }

        if (Cotacao.ExigeMoedaEstrangeira(modalidade))
        {
            // Finimp/Lei4131: operador escolhe; default Usd preserva o comportamento legado.
            return cmd.MoedaAlvo is not null ? Enum.Parse<Moeda>(cmd.MoedaAlvo, true) : Moeda.Usd;
        }

        // Modalidades BRL puras (validator já rejeitou moedaAlvo != Brl).
        return Moeda.Brl;
    }

    private static DateOnly ToDateOnly(LocalDate d) => new(d.Year, d.Month, d.Day);
}
