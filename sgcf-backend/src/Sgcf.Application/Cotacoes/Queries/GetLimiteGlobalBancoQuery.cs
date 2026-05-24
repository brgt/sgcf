using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Retorna o limite global de banco pelo seu identificador.
/// Lança <see cref="KeyNotFoundException"/> quando o registro não existe.
/// SPEC §3.2 — Queries de LimiteGlobalBanco.
/// </summary>
public sealed record GetLimiteGlobalBancoQuery(Guid Id) : IRequest<LimiteGlobalBancoDto>;

public sealed class GetLimiteGlobalBancoQueryHandler(ILimiteGlobalBancoRepository repo)
    : IRequestHandler<GetLimiteGlobalBancoQuery, LimiteGlobalBancoDto>
{
    public async Task<LimiteGlobalBancoDto> Handle(
        GetLimiteGlobalBancoQuery query,
        CancellationToken cancellationToken)
    {
        LimiteGlobalBanco limite = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"LimiteGlobalBanco {query.Id} não encontrado.");

        return LimiteGlobalBancoDto.From(limite);
    }
}
