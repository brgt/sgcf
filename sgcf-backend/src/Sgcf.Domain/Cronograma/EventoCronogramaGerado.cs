using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Resultado intermediário e uniforme das strategies de geração de cronograma.
/// Não contém ContratoId — o orquestrador (Application) cria a entidade EventoCronograma
/// após aplicar o ajuste de dia útil via IBusinessDayCalendar.
/// </summary>
public sealed record EventoCronogramaGerado(
    int NumeroEvento,
    TipoEventoCronograma Tipo,
    LocalDate DataPrevista,
    Money Valor,
    decimal? SaldoDevedorApos
);
