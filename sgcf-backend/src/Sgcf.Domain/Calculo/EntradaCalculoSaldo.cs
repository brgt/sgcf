using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Domain.Calculo;

/// <summary>
/// Dados de entrada para o cálculo de saldo devedor em uma data de referência.
/// </summary>
public sealed record EntradaCalculoSaldo(
    Money ValorPrincipalInicial,
    Percentual TaxaAa,
    BaseCalculo BaseCalculo,
    LocalDate DataDesembolso,
    LocalDate DataReferencia,
    IReadOnlyList<EventoSaldoItem> Eventos,
    decimal? TaxaCambio = null
);

/// <summary>
/// Linha de evento do cronograma usado como entrada para o cálculo de saldo.
/// </summary>
public sealed record EventoSaldoItem(
    TipoEventoCronograma Tipo,
    StatusEventoCronograma Status,
    LocalDate DataPrevista,
    Money Valor
);
