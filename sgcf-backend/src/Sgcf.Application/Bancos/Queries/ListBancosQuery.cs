using MediatR;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Queries;

public sealed record ListBancosQuery : IRequest<IReadOnlyList<BancoDto>>;

public sealed class ListBancosQueryHandler(IBancoRepository repo)
    : IRequestHandler<ListBancosQuery, IReadOnlyList<BancoDto>>
{
    public async Task<IReadOnlyList<BancoDto>> Handle(ListBancosQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Banco> bancos = await repo.ListAllAsync(cancellationToken);
        List<BancoDto> result = new(bancos.Count);

        foreach (Banco banco in bancos)
        {
            result.Add(BancoDto.From(banco));
        }

        return result.AsReadOnly();
    }
}
