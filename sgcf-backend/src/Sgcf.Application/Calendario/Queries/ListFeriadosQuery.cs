using MediatR;

using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario.Queries;

/// <summary>
/// Lista feriados de um ano específico, opcionalmente filtrados por escopo.
/// Quando <paramref name="Escopo"/> é nulo, retorna feriados de todos os escopos.
/// </summary>
public sealed record ListFeriadosQuery(int Ano, EscopoFeriado? Escopo = null)
    : IRequest<IReadOnlyList<FeriadoDto>>;

public sealed class ListFeriadosQueryHandler(IFeriadoRepository repo)
    : IRequestHandler<ListFeriadosQuery, IReadOnlyList<FeriadoDto>>
{
    public async Task<IReadOnlyList<FeriadoDto>> Handle(
        ListFeriadosQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Feriado> feriados = await repo.ListByYearAsync(
            query.Ano, query.Escopo, cancellationToken);

        List<FeriadoDto> result = new(feriados.Count);
        foreach (Feriado f in feriados)
        {
            result.Add(FeriadoDto.From(f));
        }
        return result.AsReadOnly();
    }
}
