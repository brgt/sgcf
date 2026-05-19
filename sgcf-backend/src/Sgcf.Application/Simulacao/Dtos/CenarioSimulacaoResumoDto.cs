using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// DTO resumido de cenário para uso em listagens.
/// Omite as simulações filhas para reduzir payload — use <see cref="CenarioSimulacaoDto"/> para o detalhe completo.
/// </summary>
public sealed record CenarioSimulacaoResumoDto(
    Guid Id,
    string Nome,
    string Status,
    int AnoBase,
    int QtdeSimulacoes,
    string CriadoPor,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Projeta o agregado de domínio para este DTO resumido.</summary>
    public static CenarioSimulacaoResumoDto From(CenarioSimulacao c) =>
        new(
            c.Id,
            c.Nome,
            c.Status.ToString(),
            c.AnoBase,
            c.Simulacoes.Count,
            c.CriadoPor,
            c.UpdatedAt.ToDateTimeOffset());
}
