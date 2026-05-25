using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Projeção de leitura de <see cref="GarantiaExigidaRevisao"/> para a camada de API.
/// Retornado por <c>GET /api/v1/limites-banco/{id}/revisoes-garantias</c>.
/// SPEC §5.2.
/// </summary>
public sealed record GarantiaExigidaRevisaoDto(
    Guid Id,
    DateTimeOffset VigenciaInicio,
    DateTimeOffset? VigenciaFim,
    DateTimeOffset RegistradoEm,
    string? Motivo,
    string? Observacoes,
    IReadOnlyList<GarantiaExigidaItemDto> Itens)
{
    /// <summary>Constrói o DTO a partir da entidade de domínio.</summary>
    public static GarantiaExigidaRevisaoDto From(GarantiaExigidaRevisao r)
    {
        List<GarantiaExigidaItemDto> itens = new(r.Itens.Count);
        foreach (GarantiaExigidaItem item in r.Itens)
        {
            itens.Add(GarantiaExigidaItemDto.From(item));
        }

        return new GarantiaExigidaRevisaoDto(
            r.Id,
            r.VigenciaInicio.ToDateTimeOffset(),
            r.VigenciaFim?.ToDateTimeOffset(),
            r.RegistradoEm.ToDateTimeOffset(),
            r.Motivo,
            r.Observacoes,
            itens.AsReadOnly());
    }
}
