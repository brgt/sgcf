using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// DTO completo de saída para um cenário de simulação, incluindo todas as simulações filhas.
/// Usado nas respostas de Create, Get, Atualizar, Ativar, Arquivar e Duplicar.
/// </summary>
public sealed record CenarioSimulacaoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    int AnoBase,
    string Status,
    string CriadoPor,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SimulacaoContratacaoDto> Simulacoes)
{
    /// <summary>Projeta o agregado de domínio para este DTO.</summary>
    public static CenarioSimulacaoDto From(CenarioSimulacao c)
    {
        List<SimulacaoContratacaoDto> simulacoes = new(c.Simulacoes.Count);
        foreach (var s in c.Simulacoes)
        {
            simulacoes.Add(SimulacaoContratacaoDto.From(s));
        }

        return new CenarioSimulacaoDto(
            c.Id,
            c.Nome,
            c.Descricao,
            c.AnoBase,
            c.Status.ToString(),
            c.CriadoPor,
            c.CreatedAt.ToDateTimeOffset(),
            c.UpdatedAt.ToDateTimeOffset(),
            simulacoes.AsReadOnly());
    }
}
