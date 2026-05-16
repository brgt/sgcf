using NodaTime;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Filtros para listagem paginada de cotações.
/// Todos os campos são opcionais; quando omitidos a dimensão não é filtrada.
/// </summary>
public sealed record CotacaoFilter(
    StatusCotacao? Status = null,
    ModalidadeContrato? Modalidade = null,
    LocalDate? Desde = null,
    LocalDate? Ate = null,
    int Page = 1,
    int PageSize = 20);
