namespace Sgcf.Application.Auditoria;

/// <summary>
/// Parâmetros de filtro e paginação para consulta de eventos de auditoria.
/// Todos os campos são opcionais; quando omitidos, a dimensão correspondente não é filtrada.
/// </summary>
public sealed record AuditFilter(
    string? Entity = null,
    Guid? EntityId = null,
    string? ActorSub = null,
    string? Source = null,
    string? Operation = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null,
    int Page = 1,
    int PageSize = 50);
