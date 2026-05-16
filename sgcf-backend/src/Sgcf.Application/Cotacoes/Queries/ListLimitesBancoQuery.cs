using MediatR;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>Lista limites operacionais por banco/modalidade. SPEC §6.2.</summary>
public sealed record ListLimitesBancoQuery(
    Guid? BancoId = null,
    string? Modalidade = null) : IRequest<IReadOnlyList<LimiteBancoDto>>;

public sealed class ListLimitesBancoQueryHandler(ILimiteBancoRepository repo)
    : IRequestHandler<ListLimitesBancoQuery, IReadOnlyList<LimiteBancoDto>>
{
    public async Task<IReadOnlyList<LimiteBancoDto>> Handle(
        ListLimitesBancoQuery query,
        CancellationToken cancellationToken)
    {
        ModalidadeContrato? modalidade = query.Modalidade is not null
            && Enum.TryParse<ModalidadeContrato>(query.Modalidade, true, out ModalidadeContrato m)
            ? m : null;

        IReadOnlyList<LimiteBanco> limites = await repo.ListAsync(query.BancoId, modalidade, cancellationToken);

        List<LimiteBancoDto> result = new(limites.Count);
        foreach (LimiteBanco l in limites)
        {
            result.Add(LimiteBancoDto.From(l));
        }

        return result.AsReadOnly();
    }
}
