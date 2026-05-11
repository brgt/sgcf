using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo CDB Cativo — extensão 1:1 com <see cref="Garantia"/>.
/// Armazena informações específicas do CDB bloqueado como colateral.
/// </summary>
public sealed class GarantiaCdbCativoDetail : Entity
{
    public Guid GarantiaId { get; private set; }
    public string BancoCustodia { get; private set; } = default!;
    public string NumeroCdb { get; private set; } = default!;
    public LocalDate DataEmissaoCdb { get; private set; }
    public LocalDate DataVencimentoCdb { get; private set; }

    /// <summary>Rendimento anual do CDB armazenado como fração (0..1). Null quando não informado.</summary>
    internal decimal? RendimentoAaDecimal { get; private set; }

    /// <summary>Rendimento anual do CDB como <see cref="Percentual"/> tipado. Null quando não informado.</summary>
    public Percentual? RendimentoAa =>
        RendimentoAaDecimal.HasValue ? Percentual.DeFracao(RendimentoAaDecimal.Value) : null;

    /// <summary>Percentual do CDI armazenado como fração (0..1). Null quando não informado.</summary>
    internal decimal? PercentualCdiDecimal { get; private set; }

    /// <summary>Percentual do CDI como <see cref="Percentual"/> tipado. Null quando não informado.</summary>
    public Percentual? PercentualCdi =>
        PercentualCdiDecimal.HasValue ? Percentual.DeFracao(PercentualCdiDecimal.Value) : null;

    /// <summary>Alíquota de IRRF na aplicação armazenada como fração (0..1). Null quando não informada.</summary>
    internal decimal? TaxaIrrfAplicacaoDecimal { get; private set; }

    /// <summary>Alíquota de IRRF na aplicação como <see cref="Percentual"/> tipado. Null quando não informada.</summary>
    public Percentual? TaxaIrrfAplicacao =>
        TaxaIrrfAplicacaoDecimal.HasValue ? Percentual.DeFracao(TaxaIrrfAplicacaoDecimal.Value) : null;

    private GarantiaCdbCativoDetail() { }

    /// <summary>
    /// Cria um novo detalhe de CDB cativo.
    /// Os parâmetros de percentual são fornecidos como valor humano (ex: 13.5 para 13,5% a.a.).
    /// </summary>
    public static GarantiaCdbCativoDetail Criar(
        Guid garantiaId,
        string bancoCustodia,
        string numeroCdb,
        LocalDate dataEmissaoCdb,
        LocalDate dataVencimentoCdb,
        decimal? rendimentoAaPct,
        decimal? percentualCdiPct,
        decimal? taxaIrrfAplicacaoPct)
    {
        if (string.IsNullOrWhiteSpace(bancoCustodia))
        {
            throw new ArgumentException("BancoCustodia não pode ser vazio.", nameof(bancoCustodia));
        }

        if (string.IsNullOrWhiteSpace(numeroCdb))
        {
            throw new ArgumentException("NumeroCdb não pode ser vazio.", nameof(numeroCdb));
        }

        if (dataVencimentoCdb < dataEmissaoCdb)
        {
            throw new ArgumentException(
                "DataVencimentoCdb não pode ser anterior a DataEmissaoCdb.", nameof(dataVencimentoCdb));
        }

        return new GarantiaCdbCativoDetail
        {
            GarantiaId = garantiaId,
            BancoCustodia = bancoCustodia,
            NumeroCdb = numeroCdb,
            DataEmissaoCdb = dataEmissaoCdb,
            DataVencimentoCdb = dataVencimentoCdb,
            RendimentoAaDecimal = rendimentoAaPct.HasValue ? rendimentoAaPct.Value / 100m : (decimal?)null,
            PercentualCdiDecimal = percentualCdiPct.HasValue ? percentualCdiPct.Value / 100m : (decimal?)null,
            TaxaIrrfAplicacaoDecimal = taxaIrrfAplicacaoPct.HasValue ? taxaIrrfAplicacaoPct.Value / 100m : (decimal?)null
        };
    }
}
