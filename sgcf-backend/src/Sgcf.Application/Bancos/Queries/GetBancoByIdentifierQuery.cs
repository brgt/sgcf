using MediatR;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Queries;

/// <summary>
/// Resolve um banco por codigoCompe (exato), apelido (exato, case-insensitive)
/// ou texto parcial em qualquer campo — nessa ordem de prioridade.
/// </summary>
public sealed record GetBancoByIdentifierQuery(string Identifier) : IRequest<BancoDto>;

public sealed class GetBancoByIdentifierQueryHandler(IBancoRepository repo)
    : IRequestHandler<GetBancoByIdentifierQuery, BancoDto>
{
    public async Task<BancoDto> Handle(GetBancoByIdentifierQuery query, CancellationToken cancellationToken)
    {
        string id = query.Identifier.Trim();

        IReadOnlyList<Banco> filtered = await repo.ListFilteredAsync(id, cancellationToken);

        Banco banco =
            await repo.GetByCodigoCompeAsync(id, cancellationToken) ??
            await repo.GetByApelidoAsync(id, cancellationToken) ??
            (filtered.Count > 0 ? filtered[0] : null) ??
            throw new KeyNotFoundException($"Banco '{id}' não encontrado.");

        return BancoDto.From(banco);
    }
}
