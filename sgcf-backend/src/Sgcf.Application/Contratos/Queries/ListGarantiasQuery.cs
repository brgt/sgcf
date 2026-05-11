using MediatR;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

/// <summary>Retorna todas as garantias de um contrato.</summary>
public sealed record ListGarantiasQuery(Guid ContratoId) : IRequest<IReadOnlyList<GarantiaDto>>;

public sealed class ListGarantiasQueryHandler(
    IContratoRepository contratoRepo,
    IGarantiaRepository garantiaRepo)
    : IRequestHandler<ListGarantiasQuery, IReadOnlyList<GarantiaDto>>
{
    public async Task<IReadOnlyList<GarantiaDto>> Handle(
        ListGarantiasQuery query,
        CancellationToken cancellationToken)
    {
        // Validate contrato exists
        Contrato contrato = await contratoRepo.GetByIdAsync(query.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{query.ContratoId}' não encontrado.");

        IReadOnlyList<Garantia> garantias = await garantiaRepo.ListByContratoAsync(
            query.ContratoId, cancellationToken);

        List<GarantiaDto> dtos = new(garantias.Count);
        foreach (Garantia g in garantias)
        {
            dtos.Add(MapToDto(g));
        }

        return dtos.AsReadOnly();
    }

    private static GarantiaDto MapToDto(Garantia g) =>
        new(
            Id: g.Id,
            ContratoId: g.ContratoId,
            Tipo: g.Tipo.ToString(),
            ValorBrl: g.ValorBrl.Valor,
            PercentualPrincipalPct: g.PercentualPrincipal?.AsHumano,
            DataConstituicao: g.DataConstituicao.ToString(),
            DataLiberacaoPrevista: g.DataLiberacaoPrevista?.ToString(),
            DataLiberacaoEfetiva: g.DataLiberacaoEfetiva?.ToString(),
            Status: g.Status.ToString(),
            Observacoes: g.Observacoes,
            Alertas: Array.Empty<string>(),
            // Detail navigation not loaded in list query — detail endpoints should be separate if needed
            Cdb: null,
            Sblc: null,
            Aval: null,
            Alienacao: null,
            Duplicatas: null,
            Recebiveis: null,
            Boleto: null,
            FgiDetalhe: null);
}
