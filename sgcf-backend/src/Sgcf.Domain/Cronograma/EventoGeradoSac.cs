using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Evento financeiro gerado pelo <see cref="SacStrategy"/> para um cronograma SAC.
/// Cada parcela produz dois ou três eventos: Juros, Principal e, opcionalmente, IrrfRetido.
/// </summary>
public sealed record EventoGeradoSac(
    int NumeroParcela,
    TipoEventoCronograma Tipo,
    LocalDate DataPrevista,
    Money Valor,
    decimal? SaldoDevedorApos
);
