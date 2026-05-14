using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Evento financeiro gerado pelo <see cref="BulletComJurosPeriodicosStrategy"/>.
/// Coupons intermediários têm <c>SaldoDevedorApos</c> nulo; o último PRINCIPAL tem zero.
/// </summary>
public sealed record EventoGeradoBulletComJuros(
    int NumeroEvento,
    TipoEventoCronograma Tipo,
    LocalDate DataPrevista,
    Money Valor,
    decimal? SaldoDevedorApos
);
