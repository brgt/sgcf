using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Lista todos os limites globais de banco, com filtros opcionais.
/// Retorna lista vazia (nunca 404) quando nenhum registro atende aos filtros.
/// SPEC §3.2 — Queries de LimiteGlobalBanco.
/// </summary>
public sealed record ListarLimitesGlobaisBancoQuery(
    Guid? BancoId = null,
    DateOnly? VigentesEm = null) : IRequest<IReadOnlyList<LimiteGlobalBancoDto>>;

public sealed class ListarLimitesGlobaisBancoQueryHandler(ILimiteGlobalBancoRepository repo)
    : IRequestHandler<ListarLimitesGlobaisBancoQuery, IReadOnlyList<LimiteGlobalBancoDto>>
{
    public async Task<IReadOnlyList<LimiteGlobalBancoDto>> Handle(
        ListarLimitesGlobaisBancoQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate? vigentesEm = query.VigentesEm.HasValue
            ? new LocalDate(query.VigentesEm.Value.Year, query.VigentesEm.Value.Month, query.VigentesEm.Value.Day)
            : null;

        IReadOnlyList<LimiteGlobalBanco> limites = await repo.ListAsync(
            query.BancoId, vigentesEm, cancellationToken);

        List<LimiteGlobalBancoDto> result = new(limites.Count);
        foreach (LimiteGlobalBanco l in limites)
        {
            result.Add(LimiteGlobalBancoDto.From(l));
        }

        return result.AsReadOnly();
    }
}
