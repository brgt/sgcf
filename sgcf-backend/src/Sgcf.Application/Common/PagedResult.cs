namespace Sgcf.Application.Common;

/// <summary>
/// Envelope de paginação genérico retornado por queries que suportam filtros e paging.
/// Inclui os itens da página atual, o total de registros que satisfazem o filtro,
/// e os parâmetros de paginação aplicados.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
