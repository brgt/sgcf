using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>Retorna um limite operacional pelo seu identificador. SPEC §6.2.</summary>
public sealed record GetLimiteBancoByIdQuery(Guid Id) : IRequest<LimiteBancoDto>;

public sealed class GetLimiteBancoByIdQueryHandler(ILimiteBancoRepository repo)
    : IRequestHandler<GetLimiteBancoByIdQuery, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(GetLimiteBancoByIdQuery query, CancellationToken cancellationToken)
    {
        LimiteBanco limite = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{query.Id}' não encontrado.");

        return LimiteBancoDto.From(limite);
    }
}
