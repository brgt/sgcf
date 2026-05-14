using NodaTime;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cronograma;

public sealed record GerarCronogramaInput(
    Money ValorPrincipal,
    Percentual TaxaAa,
    BaseCalculo BaseCalculo,
    LocalDate DataDesembolso,
    LocalDate DataPrimeiroVencimento,
    int QuantidadeParcelas,
    Periodicidade Periodicidade,
    AnchorDiaMes AnchorDiaMes,
    int? AnchorDiaFixo,
    Periodicidade? PeriodicidadeJuros,
    ConvencaoDataNaoUtil ConvencaoDataNaoUtil,
    Percentual? AliqIrrf = null,
    Percentual? AliqIofCambio = null,
    decimal? TarifaRofBrl = null,
    decimal? TarifaCadempBrl = null
);
