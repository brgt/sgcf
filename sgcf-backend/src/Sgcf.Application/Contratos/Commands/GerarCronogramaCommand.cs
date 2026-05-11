using FluentValidation;
using MediatR;
using NodaTime;
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
    IGeradorCronograma geradorCronograma,
    IGerarSacStrategy geradorSac)
    : IRequestHandler<GerarCronogramaCommand, IReadOnlyList<EventoCronogramaDto>>
{
    private const int DefaultNumeroParcelas = 4;

    public async Task<IReadOnlyList<EventoCronogramaDto>> Handle(GerarCronogramaCommand cmd, CancellationToken cancellationToken)
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

        await cronogramaRepo.DeleteAllByContratoIdAsync(cmd.ContratoId, cancellationToken);

        List<EventoCronograma> entities;

        if (contrato.Modalidade == ModalidadeContrato.Fgi)
        {
            FgiDetail? fgiDetail = await contratoRepo.GetFgiDetailAsync(cmd.ContratoId, cancellationToken);
            entities = await GerarEntidadesFgiAsync(cmd, contrato, fgiDetail);
        }
        else
        {
            entities = contrato.Modalidade switch
            {
                ModalidadeContrato.Finimp => GerarEntidadesBullet(cmd, contrato),
                ModalidadeContrato.Nce => GerarEntidadesBullet(cmd, contrato),
                ModalidadeContrato.Lei4131 => GerarEntidadesSac(cmd, contrato),
                ModalidadeContrato.BalcaoCaixa => throw new ArgumentException(
                    "Balcão Caixa não suporta geração automática de cronograma. Use o endpoint importar-cronograma.",
                    nameof(cmd)),
                _ => throw new ArgumentException(
                    $"Modalidade {contrato.Modalidade} não suporta geração automática de cronograma.",
                    nameof(cmd))
            };
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

    private List<EventoCronograma> GerarEntidadesBullet(GerarCronogramaCommand cmd, Contrato contrato)
    {
        EntradaBullet entrada = new(
            ValorPrincipal: contrato.ValorPrincipal,
            TaxaAa: contrato.TaxaAa,
            BaseCalculo: contrato.BaseCalculo,
            DataDesembolso: contrato.DataContratacao,
            DataVencimento: contrato.DataVencimento,
            AliqIrrf: cmd.AliqIrrfPct.HasValue ? Percentual.De(cmd.AliqIrrfPct.Value) : (Percentual?)null,
            AliqIofCambio: cmd.AliqIofCambioPct.HasValue ? Percentual.De(cmd.AliqIofCambioPct.Value) : (Percentual?)null,
            TarifaRofBrl: cmd.TarifaRofBrl,
            TarifaCadempBrl: cmd.TarifaCadempBrl);

        IReadOnlyList<EventoGeradoBullet> gerados = geradorCronograma.Gerar(entrada);

        List<EventoCronograma> entities = new(gerados.Count);
        foreach (EventoGeradoBullet gerado in gerados)
        {
            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: gerado.NumeroEvento,
                tipo: gerado.Tipo,
                dataPrevista: gerado.DataPrevista,
                valorMoedaOriginal: gerado.Valor,
                saldoDevedorApos: gerado.SaldoDevedorApos));
        }

        return entities;
    }

    private List<EventoCronograma> GerarEntidadesSac(GerarCronogramaCommand cmd, Contrato contrato)
    {
        int numeroParcelas = cmd.NumeroParcelas ?? DefaultNumeroParcelas;

        EntradaSac entrada = new(
            ValorPrincipal: contrato.ValorPrincipal,
            TaxaAa: contrato.TaxaAa,
            BaseCalculo: contrato.BaseCalculo,
            DataDesembolso: contrato.DataContratacao,
            DataVencimento: contrato.DataVencimento,
            NumeroParcelas: numeroParcelas,
            AliqIrrf: cmd.AliqIrrfPct.HasValue ? Percentual.De(cmd.AliqIrrfPct.Value) : (Percentual?)null);

        IReadOnlyList<EventoGeradoSac> gerados = geradorSac.GerarSac(entrada);

        List<EventoCronograma> entities = new(gerados.Count);
        foreach (EventoGeradoSac gerado in gerados)
        {
            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: (short)gerado.NumeroParcela,
                tipo: gerado.Tipo,
                dataPrevista: gerado.DataPrevista,
                valorMoedaOriginal: gerado.Valor,
                saldoDevedorApos: gerado.SaldoDevedorApos));
        }

        return entities;
    }

    // FGI: Bullet strategy + optional TarifaFgi event at vencimento.
    // taxaFgiAa priority: command param overrides detail value.
    private Task<List<EventoCronograma>> GerarEntidadesFgiAsync(
        GerarCronogramaCommand cmd,
        Contrato contrato,
        FgiDetail? fgiDetail)
    {
        List<EventoCronograma> entities = GerarEntidadesBullet(cmd, contrato);

        // Resolve taxa FGI: command param takes precedence over persisted detail
        Percentual? taxaFgi = cmd.TaxaFgiAaPct.HasValue
            ? Percentual.De(cmd.TaxaFgiAaPct.Value)
            : fgiDetail?.TaxaFgiAa;

        if (taxaFgi.HasValue)
        {
            int prazo = Period.Between(contrato.DataContratacao, contrato.DataVencimento, PeriodUnits.Days).Days;
            decimal baseCalculo = (decimal)contrato.BaseCalculo;

            decimal valorTarifaFgi = Math.Round(
                contrato.ValorPrincipal.Valor * taxaFgi.Value.AsDecimal * prazo / baseCalculo,
                2,
                MidpointRounding.AwayFromZero);

            entities.Add(EventoCronograma.Criar(
                contratoId: cmd.ContratoId,
                numeroEvento: 1,
                tipo: TipoEventoCronograma.TarifaFgi,
                dataPrevista: contrato.DataVencimento,
                valorMoedaOriginal: new Money(valorTarifaFgi, contrato.ValorPrincipal.Moeda),
                saldoDevedorApos: null));
        }

        return Task.FromResult(entities);
    }
}
