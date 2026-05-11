using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Evento financeiro gerado pelo BulletStrategy para um cronograma de amortização bullet.
/// </summary>
public sealed record EventoGeradoBullet(
    short NumeroEvento,
    TipoEventoCronograma Tipo,
    LocalDate DataPrevista,
    Money Valor,
    decimal? SaldoDevedorApos
);
