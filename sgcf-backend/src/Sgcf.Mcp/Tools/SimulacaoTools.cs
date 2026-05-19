using System.ComponentModel;
using System.Text.Json;

using MediatR;

using ModelContextProtocol.Server;

using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Mcp.Tools;

/// <summary>
/// Tools read-only para que agentes externos consultem o Quadro da Dívida e
/// os Cenários de Simulação sem necessidade de acesso direto à API HTTP.
///
/// AD-4.4: cada método delega ao handler MediatR correspondente — sem duplicação
/// de lógica de domínio. Apenas a camada de tradução (string → Guid/Enum) fica aqui.
/// </summary>
[McpServerToolType]
public sealed class SimulacaoTools(IMediator mediator)
{
    /// <summary>
    /// Consulta o Quadro da Dívida (saldo mês a mês por banco) para um ano civil.
    /// Opcionalmente aplica um cenário de simulação para incorporar captações hipotéticas.
    /// </summary>
    [McpServerTool(Name = "get_quadro_divida")]
    [Description(
        "Consulta o Quadro da Dívida (saldo mês a mês por banco) para um ano. " +
        "Opcionalmente aplica um cenário de simulação. " +
        "Retorna: snapshot inicial + projeção 12 meses + sumário anual + alertas.")]
    public async Task<string> GetQuadroDividaAsync(
        [Description("Ano da projeção (ex: 2026).")] int ano,
        [Description("Id (UUID) do cenário de simulação (opcional).")] string? cenarioId,
        CancellationToken cancellationToken)
    {
        Guid? cenarioGuid = null;

        if (!string.IsNullOrEmpty(cenarioId))
        {
            if (!Guid.TryParse(cenarioId, out Guid parsed))
            {
                return JsonSerializer.Serialize(
                    new { error = "cenarioId inválido — esperado UUID." },
                    McpJsonOptions.Default);
            }

            cenarioGuid = parsed;
        }

        try
        {
            QuadroDividaDto resultado = await mediator.Send(
                new GetQuadroDividaQuery(ano, cenarioGuid),
                cancellationToken);

            return JsonSerializer.Serialize(resultado, McpJsonOptions.Default);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
    }

    /// <summary>
    /// Lista cenários de simulação cadastrados com filtros opcionais.
    /// Retorna resumo (id, nome, status, anoBase, qtdeSimulacoes) sem as simulações filhas.
    /// </summary>
    [McpServerTool(Name = "list_cenarios_simulacao")]
    [Description(
        "Lista cenários de simulação cadastrados, com filtros opcionais. " +
        "Retorna apenas resumo (id, nome, status, anoBase, qtdeSimulacoes). " +
        "Status aceitos: Rascunho, Ativo, Arquivado.")]
    public async Task<string> ListCenariosSimulacaoAsync(
        [Description("Filtro de status: Rascunho, Ativo ou Arquivado (opcional).")] string? status,
        [Description("Filtro de ano-base do cenário (opcional).")] int? anoBase,
        CancellationToken cancellationToken)
    {
        StatusCenarioSimulacao? statusEnum = null;

        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<StatusCenarioSimulacao>(status, ignoreCase: true, out StatusCenarioSimulacao parsed))
            {
                return JsonSerializer.Serialize(
                    new { error = $"status inválido: '{status}'. Valores aceitos: Rascunho, Ativo, Arquivado." },
                    McpJsonOptions.Default);
            }

            statusEnum = parsed;
        }

        IReadOnlyList<Application.Simulacao.Dtos.CenarioSimulacaoResumoDto> resultado =
            await mediator.Send(
                new ListCenariosSimulacaoQuery(statusEnum, anoBase, null),
                cancellationToken);

        return JsonSerializer.Serialize(resultado, McpJsonOptions.Default);
    }

    /// <summary>
    /// Consulta um cenário de simulação completo pelo seu identificador único,
    /// incluindo todas as simulações de contratação filhas.
    /// </summary>
    [McpServerTool(Name = "get_cenario_simulacao")]
    [Description(
        "Consulta um cenário de simulação completo, incluindo todas as simulações filhas. " +
        "Retorna KeyNotFound (via campo 'error') quando o id não existe.")]
    public async Task<string> GetCenarioSimulacaoAsync(
        [Description("Id (UUID) do cenário de simulação.")] string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out Guid cenarioGuid))
        {
            return JsonSerializer.Serialize(
                new { error = "id inválido — esperado UUID." },
                McpJsonOptions.Default);
        }

        try
        {
            Application.Simulacao.Dtos.CenarioSimulacaoDto resultado =
                await mediator.Send(
                    new GetCenarioSimulacaoByIdQuery(cenarioGuid),
                    cancellationToken);

            return JsonSerializer.Serialize(resultado, McpJsonOptions.Default);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
    }
}
