using MediatR;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>
/// Lista contas bancárias do tenant corrente.
/// Quando <paramref name="ApenasAtivas"/> é <c>true</c>, filtra somente as ativas.
/// Quando <c>null</c>, retorna todas (ativas e inativas).
/// </summary>
public sealed record ListContasBancariasQuery(bool? ApenasAtivas) : IRequest<IReadOnlyList<ContaBancariaDto>>;

public sealed class ListContasBancariasQueryHandler(IContaBancariaRepository repo)
    : IRequestHandler<ListContasBancariasQuery, IReadOnlyList<ContaBancariaDto>>
{
    public async Task<IReadOnlyList<ContaBancariaDto>> Handle(
        ListContasBancariasQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Tesouraria.ContaBancaria> contas =
            await repo.ListAsync(query.ApenasAtivas, cancellationToken);

        return contas
            .Select(ContaBancariaDto.From)
            .ToList()
            .AsReadOnly();
    }
}
