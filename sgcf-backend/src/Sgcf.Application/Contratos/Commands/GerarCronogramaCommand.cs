using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos.Commands;

public sealed record GerarCronogramaCommand(
    Guid ContratoId,
    decimal? AliqIrrfPct,
    decimal? AliqIofCambioPct,
    decimal? TarifaRofBrl,
    decimal? TarifaCadempBrl,
    int? NumeroParcelas = null,
    decimal? TaxaFgiAaPct = null
) : IRequest<IReadOnlyList<EventoCronogramaDto>>;

public sealed class GerarCronogramaCommandValidator : AbstractValidator<GerarCronogramaCommand>
{
    public GerarCronogramaCommandValidator()
    {
        RuleFor(c => c.ContratoId)
            .NotEmpty()
            .WithMessage("ContratoId não pode ser vazio.");

        RuleFor(c => c.AliqIrrfPct)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.AliqIrrfPct.HasValue)
            .WithMessage("AliqIrrfPct não pode ser negativo.");

        RuleFor(c => c.AliqIofCambioPct)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.AliqIofCambioPct.HasValue)
            .WithMessage("AliqIofCambioPct não pode ser negativo.");

        RuleFor(c => c.TarifaRofBrl)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.TarifaRofBrl.HasValue)
            .WithMessage("TarifaRofBrl não pode ser negativo.");

        RuleFor(c => c.TarifaCadempBrl)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.TarifaCadempBrl.HasValue)
            .WithMessage("TarifaCadempBrl não pode ser negativo.");

        RuleFor(c => c.NumeroParcelas)
            .GreaterThan(0)
            .When(c => c.NumeroParcelas.HasValue)
            .WithMessage("NumeroParcelas deve ser maior que zero.");

        RuleFor(c => c.TaxaFgiAaPct)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.TaxaFgiAaPct.HasValue)
            .WithMessage("TaxaFgiAaPct não pode ser negativo.");
    }
}

public sealed class GerarCronogramaCommandHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IBusinessDayCalendar calendar)
    : IRequestHandler<GerarCronogramaCommand, IReadOnlyList<EventoCronogramaDto>>
{
    public async Task<IReadOnlyList<EventoCronogramaDto>> Handle(
        GerarCronogramaCommand cmd,
        CancellationToken cancellationToken)
    {
        Contrato contrato = await contratoRepo.GetByIdAsync(cmd.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{cmd.ContratoId}' não encontrado.");

        // NCE: reject IRRF and IOF câmbio — NCE is export credit, tax-exempt
        if (contrato.Modalidade == ModalidadeContrato.Nce)
        {
            if (cmd.AliqIrrfPct.HasValue && cmd.AliqIrrfPct.Value != 0m)
            {
                throw new ArgumentException("NCE não tem IRRF — aliqIrrfPct deve ser nulo ou zero.", nameof(cmd));
            }

            if (cmd.AliqIofCambioPct.HasValue && cmd.AliqIofCambioPct.Value != 0m)
            {
                throw new ArgumentException("NCE não tem IOF câmbio — aliqIofCambioPct deve ser nulo ou zero.", nameof(cmd));
            }
        }

        if (contrato.Modalidade == ModalidadeContrato.BalcaoCaixa)
        {
            throw new ArgumentException(
                "Balcão Caixa não suporta geração automática de cronograma. Use o endpoint importar-cronograma.",
                nameof(cmd));
        }

        await cronogramaRepo.DeleteAllByContratoIdAsync(cmd.ContratoId, cancellationToken);

        GerarCronogramaInput strategyInput = BuildInput(cmd, contrato);
        ICronogramaStrategy strategy = CronogramaStrategyFactory.Criar(contrato.EstruturaAmortizacao);
        IReadOnlyList<EventoCronogramaGerado> gerados = strategy.Gerar(strategyInput);

        List<EventoCronograma> entities = new(gerados.Count);
        foreach (EventoCronogramaGerado g in gerados)
        {
            LocalDate dataAjustada = await calendar.AjustarPorConvencaoAsync(
                g.DataPrevista, contrato.ConvencaoDataNaoUtil, cancellationToken);

            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: (short)g.NumeroEvento,
                tipo: g.Tipo,
                dataPrevista: dataAjustada,
                valorMoedaOriginal: g.Valor,
                saldoDevedorApos: g.SaldoDevedorApos));
        }

        if (contrato.Modalidade == ModalidadeContrato.Fgi)
        {
            await AdicionarTarifaFgiAsync(cmd, contrato, entities, cancellationToken);
        }

        cronogramaRepo.AddRange(entities);
        await cronogramaRepo.SaveChangesAsync(cancellationToken);

        List<EventoCronogramaDto> result = new(entities.Count);
        foreach (EventoCronograma e in entities)
        {
            result.Add(new EventoCronogramaDto(
                NumeroEvento: e.NumeroEvento,
                Tipo: e.Tipo.ToString(),
                DataPrevista: new DateOnly(e.DataPrevista.Year, e.DataPrevista.Month, e.DataPrevista.Day),
                Valor: e.ValorMoedaOriginal.Valor,
                Moeda: e.ValorMoedaOriginal.Moeda.ToString(),
                SaldoDevedorApos: e.SaldoDevedorApos?.Valor,
                Status: e.Status.ToString(),
                DataPagamentoEfetivo: null,
                ValorPagamentoEfetivo: null));
        }

        return result.AsReadOnly();
    }

    private static GerarCronogramaInput BuildInput(GerarCronogramaCommand cmd, Contrato contrato) =>
        new(
            ValorPrincipal: contrato.ValorPrincipal,
            TaxaAa: contrato.TaxaAa,
            BaseCalculo: contrato.BaseCalculo,
            DataDesembolso: contrato.DataContratacao,
            DataPrimeiroVencimento: contrato.DataPrimeiroVencimento,
            QuantidadeParcelas: contrato.QuantidadeParcelas,
            Periodicidade: contrato.Periodicidade,
            AnchorDiaMes: contrato.AnchorDiaMes,
            AnchorDiaFixo: contrato.AnchorDiaFixo,
            PeriodicidadeJuros: contrato.PeriodicidadeJuros,
            ConvencaoDataNaoUtil: contrato.ConvencaoDataNaoUtil,
            AliqIrrf: cmd.AliqIrrfPct.HasValue ? Percentual.De(cmd.AliqIrrfPct.Value) : null,
            AliqIofCambio: cmd.AliqIofCambioPct.HasValue ? Percentual.De(cmd.AliqIofCambioPct.Value) : null,
            TarifaRofBrl: cmd.TarifaRofBrl,
            TarifaCadempBrl: cmd.TarifaCadempBrl);

    private async Task AdicionarTarifaFgiAsync(
        GerarCronogramaCommand cmd,
        Contrato contrato,
        List<EventoCronograma> entities,
        CancellationToken cancellationToken)
    {
        FgiDetail? fgiDetail = await contratoRepo.GetFgiDetailAsync(cmd.ContratoId, cancellationToken);

        Percentual? taxaFgi = cmd.TaxaFgiAaPct.HasValue
            ? Percentual.De(cmd.TaxaFgiAaPct.Value)
            : fgiDetail?.TaxaFgiAa;

        if (!taxaFgi.HasValue)
        {
            return;
        }

        int prazo = Period.Between(contrato.DataContratacao, contrato.DataVencimento, PeriodUnits.Days).Days;
        decimal baseCalculo = (decimal)contrato.BaseCalculo;

        decimal valorTarifaFgi = Math.Round(
            contrato.ValorPrincipal.Valor * taxaFgi.Value.AsDecimal * prazo / baseCalculo,
            2,
            MidpointRounding.AwayFromZero);

        LocalDate dataVencAjustada = await calendar.AjustarPorConvencaoAsync(
            contrato.DataVencimento, contrato.ConvencaoDataNaoUtil, cancellationToken);

        entities.Add(EventoCronograma.Criar(
            contratoId: cmd.ContratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.TarifaFgi,
            dataPrevista: dataVencAjustada,
            valorMoedaOriginal: new Money(valorTarifaFgi, contrato.ValorPrincipal.Moeda),
            saldoDevedorApos: null));
    }
}
