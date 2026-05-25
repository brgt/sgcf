using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Lista todas as revisões de garantias exigidas de um <see cref="LimiteBanco"/>,
/// ordenadas por VigenciaInicio ascendente (SLB-05).
/// Retorna <see cref="KeyNotFoundException"/> se o limite não existir.
/// SPEC §5.1 — <c>GET /api/v1/limites-banco/{id}/revisoes-garantias</c>.
/// </summary>
public sealed record ListarRevisoesGarantiasQuery(Guid LimiteBancoId)
    : IRequest<ListarRevisoesGarantiasResponse>;

/// <summary>Payload de resposta do endpoint de histórico de revisões.</summary>
public sealed record ListarRevisoesGarantiasResponse(
    Guid LimiteBancoId,
    IReadOnlyList<GarantiaExigidaRevisaoDto> Revisoes);

public sealed class ListarRevisoesGarantiasQueryHandler(ILimiteBancoRepository repo)
    : IRequestHandler<ListarRevisoesGarantiasQuery, ListarRevisoesGarantiasResponse>
{
    /// <inheritdoc/>
    public async Task<ListarRevisoesGarantiasResponse> Handle(
        ListarRevisoesGarantiasQuery query,
        CancellationToken cancellationToken)
    {
        bool exists = await repo.ExistsAsync(query.LimiteBancoId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Limite '{query.LimiteBancoId}' não encontrado.");
        }

        IReadOnlyList<GarantiaExigidaRevisao> revisoes =
            await repo.GetRevisoesGarantiasAsync(query.LimiteBancoId, cancellationToken);

        List<GarantiaExigidaRevisaoDto> dtos = new(revisoes.Count);
        foreach (GarantiaExigidaRevisao r in revisoes)
        {
            dtos.Add(GarantiaExigidaRevisaoDto.From(r));
        }

        return new ListarRevisoesGarantiasResponse(query.LimiteBancoId, dtos.AsReadOnly());
    }
}
