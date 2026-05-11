using MediatR;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Queries;

public sealed record GetBancoQuery(Guid Id) : IRequest<BancoDto>;

public sealed class GetBancoQueryHandler(IBancoRepository repo)
    : IRequestHandler<GetBancoQuery, BancoDto>
{
    public async Task<BancoDto> Handle(GetBancoQuery query, CancellationToken cancellationToken)
    {
        Banco banco = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Banco com Id '{query.Id}' não encontrado.");

        return BancoDto.From(banco);
    }
}
