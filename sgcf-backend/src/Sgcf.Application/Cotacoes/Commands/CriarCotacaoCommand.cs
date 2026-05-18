using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cria uma nova cotação em estado Rascunho.
/// Se CodigoInterno for nulo, gera automaticamente via repositório.
/// Busca PTAX D-1 via <see cref="ICotacaoFxRepository"/>. SPEC §6.1.
/// Onda 1: <see cref="ContratoMaeId"/> obrigatório para modalidade Refinimp. SPEC §4.1.
/// </summary>
public sealed record CriarCotacaoCommand(
    string? CodigoInterno,
    string Modalidade,
    decimal ValorAlvoBrl,
    int PrazoMaximoDias,
    DateOnly DataAbertura,
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

        RuleFor(c => c.PrazoMaximoDias)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PrazoMaximoDias deve ser maior ou igual a 1.");

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
    ICotacaoFxRepository fxRepo,
    IClock clock,
    IContratoRepository? contratoRepo = null) : IRequestHandler<CriarCotacaoCommand, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(CriarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        LocalDate dataAbertura = new(cmd.DataAbertura.Year, cmd.DataAbertura.Month, cmd.DataAbertura.Day);

        ModalidadeContrato modalidade = Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);

        // Onda 0 F0.1: busca PTAX apenas para modalidades cambiais (FINIMP, REFINIMP, Lei4131).
        // Modalidades BRL puras (NCE, CapitalDeGiro, FGI) não requerem conversão cambial.
        decimal? ptax = null;
        LocalDate? dataPtaxReferencia = null;

        if (Cotacao.ExigeMoedaEstrangeira(modalidade))
        {
            LocalDate dataPtax = dataAbertura.PlusDays(-1);
            CotacaoFx cotacaoFx = await fxRepo.GetMaisRecenteAsync(
                Moeda.Usd,
                TipoCotacao.PtaxD1,
                dataPtax,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"PTAX D-1 não disponível para a data {dataPtax}. Cadastre a cotação USD/BRL antes de criar a cotação.");

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

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno,
            modalidade,
            valorAlvo,
            cmd.PrazoMaximoDias,
            dataAbertura,
            dataPtaxReferencia: dataPtaxReferencia,
            ptaxUsadaUsdBrl: ptax,
            clock,
            cmd.Observacoes,
            contratoMaeId: cmd.ContratoMaeId);

        repo.Add(cotacao);
        await repo.SaveChangesAsync(cancellationToken);

        return CotacaoDto.From(cotacao);
    }
}
