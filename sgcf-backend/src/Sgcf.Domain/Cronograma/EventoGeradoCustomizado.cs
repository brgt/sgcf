using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Evento financeiro gerado pelo <see cref="CustomizadaStrategy"/> a partir de uma parcela customizada.
/// O último evento PRINCIPAL tem <c>SaldoDevedorApos</c> igual a zero.
/// </summary>
public sealed record EventoGeradoCustomizado(
    int NumeroParcela,
    TipoEventoCronograma Tipo,
    LocalDate DataPrevista,
    Money Valor,
    decimal? SaldoDevedorApos
);
