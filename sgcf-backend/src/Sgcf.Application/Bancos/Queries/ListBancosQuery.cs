using MediatR;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Queries;

/// <summary>
/// Lista todos os bancos cadastrados, com filtro opcional por texto livre.
/// O filtro aplica ILIKE simultâneo em CodigoCompe, RazaoSocial e Apelido.
/// </summary>
public sealed record ListBancosQuery(string? Search = null) : IRequest<IReadOnlyList<BancoDto>>;

public sealed class ListBancosQueryHandler(IBancoRepository repo)
    : IRequestHandler<ListBancosQuery, IReadOnlyList<BancoDto>>
{
    public async Task<IReadOnlyList<BancoDto>> Handle(ListBancosQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Banco> bancos = await repo.ListFilteredAsync(query.Search, cancellationToken);
        List<BancoDto> result = new(bancos.Count);

        foreach (Banco banco in bancos)
        {
            result.Add(BancoDto.From(banco));
        }

        return result.AsReadOnly();
    }
}
