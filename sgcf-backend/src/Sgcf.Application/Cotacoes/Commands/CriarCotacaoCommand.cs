using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes.Services;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cria uma nova cotação em estado Rascunho.
/// Se CodigoInterno for nulo, gera automaticamente via repositório.
/// Busca PTAX D-1 via <see cref="ICotacaoFxRepository"/>. SPEC §6.1.
/// S40: prazo aceito como tenor {valor, unidade}; prazoMaximoDias permanece como entrada legada.
/// Onda 1: <see cref="ContratoMaeId"/> obrigatório para modalidade Refinimp. SPEC §4.1.
/// </summary>
public sealed record CriarCotacaoCommand(
    string Modalidade,
    decimal ValorAlvoBrl,
    DateOnly DataAbertura,
    string? CodigoInterno = null,
    int? PrazoMaximoDias = null,
    int? PrazoMaximoValor = null,
    string? PrazoMaximoUnidade = null,
    string? Observacoes = null,
    Guid? ContratoMaeId = null) : IRequest<CotacaoDto>;

public sealed class CriarCotacaoCommandValidator : AbstractValidator<CriarCotacaoCommand>
{
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
    public async Task<CotacaoDto> Handle(CriarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        LocalDate dataAbertura = new(cmd.DataAbertura.Year, cmd.DataAbertura.Month, cmd.DataAbertura.Day);

        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);

        // S40 §4.1: resolve o tenor (precedência valor+unidade > dias legado; default por modalidade).
        UnidadePrazo? unidade = cmd.PrazoMaximoUnidade is null
            ? null
            : Enum.Parse<UnidadePrazo>(cmd.PrazoMaximoUnidade, true);
        ResolvedorTenor.Resultado tenor = ResolvedorTenor.Resolver(
            modalidade, cmd.PrazoMaximoValor, unidade, cmd.PrazoMaximoDias);

        // Onda 0 F0.1: busca PTAX apenas para modalidades cambiais (FINIMP, REFINIMP, Lei4131).
        // A generalização multimoeda da PTAX é tratada em S40 T8; aqui mantém-se USD.
        decimal? ptax = null;
        LocalDate? dataPtaxReferencia = null;
        Moeda moedaAlvo = Cotacao.ExigeMoedaEstrangeira(modalidade) ? Moeda.Usd : Moeda.Brl;

        if (Cotacao.ExigeMoedaEstrangeira(modalidade))
        {
            CotacaoFx cotacaoFx = await cotacaoResolver.ResolverFxAsync(
                Moeda.Usd,
                TipoCotacao.PtaxD1,
                dataAbertura,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"PTAX D-1 não disponível (fechamento {dataAbertura.PlusDays(-1)}). " +
                    "Cadastre a cotação USD/BRL antes de criar a cotação.");

            ptax = cotacaoFx.ValorVenda.Valor;
            dataPtaxReferencia = cotacaoFx.Momento.InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;
        }

        // Onda 1 REFINIMP — SPEC §4.1: valida contrato mãe quando modalidade = Refinimp.
        if (modalidade == ModalidadeContrato.Refinimp && cmd.ContratoMaeId.HasValue)
        {
            if (contratoRepo is null)
            {
                throw new InvalidOperationException(
                    "IContratoRepository é obrigatório para criar cotação da modalidade Refinimp.");
            }

            Contrato mae = await contratoRepo.GetByIdAsync(cmd.ContratoMaeId.Value, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Contrato mãe '{cmd.ContratoMaeId.Value}' não encontrado.");

            // Rejeita status finais ou inválidos para refinanciamento — SPEC §4.1 e §8.1.
            if (mae.Status is StatusContrato.Cancelado
                           or StatusContrato.Liquidado
                           or StatusContrato.RefinanciadoTotal)
            {
                throw new InvalidOperationException(
                    $"Contrato mãe '{cmd.ContratoMaeId.Value}' está em status '{mae.Status}' " +
                    "e não pode ser refinanciado.");
            }
        }

        // Gerar código interno se não informado
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

        List<AlertaDto> alertas = [];
        if (tenor.Alerta is not null)
        {
            alertas.Add(tenor.Alerta);
        }

        return CotacaoDto.From(cotacao, alertas);
    }
}
