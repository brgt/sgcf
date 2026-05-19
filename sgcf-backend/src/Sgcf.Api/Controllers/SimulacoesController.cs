using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao.Commands;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoints do módulo Simulações de Contratação.
///
/// <para>
/// SPEC §7.4 (CRUD de Cenário) e §7.5 (Simulações dentro do Cenário).
/// Task 2.5 estende Task 2.6 sem substituir o endpoint de preview.
/// </para>
///
/// <para>
/// Convenção de mapeamento de erros (consistente com demais controllers):
/// <list type="bullet">
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
///   <item><see cref="InvalidOperationException"/> → 409 Conflict</item>
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
/// </list>
/// </para>
///
/// <para>
/// AD-11: arquivar usa <see cref="Policies.Gerencial"/> (equivale ao papel Gerente
/// previsto na decisão — o projeto não define "Gerente" como string separada).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/simulacoes")]
public sealed class SimulacoesController(IMediator mediator) : ControllerBase
{
    // ── EXISTENTE (Task 2.6) ─────────────────────────────────────────────────

    /// <summary>
    /// Pré-visualiza o cronograma de uma simulação hipotética sem persistir nada.
    ///
    /// O endpoint é stateless (pure compute): constrói internamente uma
    /// <c>SimulacaoContratacao</c> temporária, delega ao motor de cronograma e descarta.
    /// Pode ser chamado sem cenário existente.
    ///
    /// Erros possíveis:
    /// - 400 — invariante de domínio violada (ex: ValorPrincipal = 0)
    ///          ou CDI+Spread sem CdiReferenciaAaPercentual.
    /// - 409 — estado interno inválido (raro em uso normal).
    /// </summary>
    /// <param name="query">
    /// Corpo com todos os campos de uma simulação (sem cenarioId) e,
    /// opcionalmente, o CDI de referência para operações indexadas ao CDI.
    /// </param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpPost("cronograma-hipotetico")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CronogramaHipoteticoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewCronograma(
        [FromBody] SimularCronogramaHipoteticoQuery query,
        CancellationToken ct)
    {
        try
        {
            CronogramaHipoteticoDto resultado = await mediator.Send(query, ct);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // ── NOVO (Task 2.5) — CRUD de Cenário ────────────────────────────────────

    /// <summary>
    /// Cria um novo cenário de simulação em status Rascunho.
    /// Idempotente via <c>Idempotency-Key</c> header (TTL 24h).
    /// SPEC §7.4. AD-11: política Escrita.
    /// </summary>
    [HttpPost("cenarios")]
    [Authorize(Policy = Policies.Escrita)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CriarCenario(
        [FromBody] CriarCenarioSimulacaoCommand command,
        CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetCenarioPorId), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lista cenários com filtros opcionais por status, anoBase e criadoPor.
    /// Retorna DTOs resumidos (sem simulações filhas) para eficiência de payload.
    /// Soft-deletados são excluídos automaticamente.
    /// SPEC §7.4. AD-11: política Leitura.
    /// </summary>
    [HttpGet("cenarios")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<CenarioSimulacaoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCenarios(
        [FromQuery] StatusCenarioSimulacao? status,
        [FromQuery] int? anoBase,
        [FromQuery] string? criadoPor,
        CancellationToken ct)
    {
        IReadOnlyList<CenarioSimulacaoResumoDto> result = await mediator.Send(
            new ListCenariosSimulacaoQuery(status, anoBase, criadoPor), ct);

        return Ok(result);
    }

    /// <summary>
    /// Retorna o cenário completo com todas as simulações filhas pelo Id.
    /// SPEC §7.4. AD-11: política Leitura.
    /// </summary>
    [HttpGet("cenarios/{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCenarioPorId(Guid id, CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new GetCenarioSimulacaoByIdQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Atualiza nome, descrição e/ou anoBase de um cenário.
    /// Permitido em Rascunho e Ativo; bloqueado em Arquivado (409).
    /// D-6 (Q1): qualquer membro da tesouraria pode editar qualquer cenário (sem owner check).
    /// SPEC §7.4. AD-11: política Escrita.
    /// </summary>
    [HttpPatch("cenarios/{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarCenario(
        Guid id,
        [FromBody] AtualizarCenarioCommand body,
        CancellationToken ct)
    {
        // Sobrescreve CenarioId do body com o valor da rota — evita inconsistência.
        AtualizarCenarioCommand cmd = body with { CenarioId = id };

        try
        {
            CenarioSimulacaoDto result = await mediator.Send(cmd, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Transita o cenário de Rascunho para Ativo.
    /// Lança 409 se o cenário já estiver em outro estado incompatível.
    /// SPEC §7.4. AD-11: política Escrita.
    /// </summary>
    [HttpPost("cenarios/{id:guid}/ativar")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Ativar(Guid id, CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new AtivarCenarioCommand(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Arquiva um cenário Ativo ou Rascunho — operação irreversível via API.
    /// AD-11: política Gerencial (equivale ao papel "Gerente" descrito na decisão;
    /// o projeto usa <see cref="Policies.Gerencial"/> como constante correspondente).
    /// SPEC §7.4.
    /// </summary>
    [HttpPost("cenarios/{id:guid}/arquivar")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Arquivar(Guid id, CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new ArquivarCenarioCommand(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cria uma cópia profunda do cenário em status Rascunho com novo Id.
    /// Nome da cópia: "{original} (cópia)". Simulações filhas são copiadas com novos Ids.
    /// Idempotente via <c>Idempotency-Key</c> header (TTL 24h).
    /// SPEC D-10 / Q7. AD-11: política Escrita.
    /// </summary>
    [HttpPost("cenarios/{id:guid}/duplicar")]
    [Authorize(Policy = Policies.Escrita)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicar(Guid id, CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new DuplicarCenarioCommand(id), ct);
            return CreatedAtAction(nameof(GetCenarioPorId), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Soft-delete do cenário. O registro permanece no banco com flag DeletadoEm preenchido.
    /// GET subsequente retorna 404. SPEC §7.4. AD-11: política Escrita.
    /// </summary>
    [HttpDelete("cenarios/{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deletar(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new DeletarCenarioCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── NOVO (Task 2.5) — Simulações dentro do Cenário ───────────────────────

    /// <summary>
    /// Adiciona uma nova simulação de contratação ao cenário.
    /// Retorna o cenário completo atualizado (incluindo a nova simulação).
    /// Idempotente via <c>Idempotency-Key</c> header (TTL 24h).
    /// Bloqueado se cenário estiver Arquivado (409).
    /// SPEC §7.5. AD-11: política Escrita.
    /// </summary>
    [HttpPost("cenarios/{id:guid}/simulacoes")]
    [Authorize(Policy = Policies.Escrita)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdicionarSimulacao(
        Guid id,
        [FromBody] AdicionarSimulacaoInput input,
        CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new AdicionarSimulacaoCommand(id, input), ct);
            return CreatedAtAction(nameof(GetCenarioPorId), new { id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // ── NOVO (Task 3.3) — Atalho Cenário → Quadro da Dívida ──────────────────

    /// <summary>
    /// Endpoint de conveniência: retorna o Quadro da Dívida com o cenário de simulação
    /// informado já aplicado.
    ///
    /// <para>
    /// Internamente executa dois passos:
    /// <list type="number">
    ///   <item>Busca o cenário via <c>GetCenarioSimulacaoByIdQuery</c> para obter <c>AnoBase</c>.</item>
    ///   <item>Delega para <c>GetQuadroDividaQuery</c> passando o ano efetivo e o <c>cenarioId</c>.
    ///        A integração completa (cenário aplicado no quadro) depende da Task 3.1;
    ///        enquanto 3.1 não estiver mergeada, o quadro retornado reflete apenas dados reais
    ///        (sem overlay das simulações do cenário) mas a rota e o schema são os corretos.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// O parâmetro opcional <c>?ano=</c> sobrepõe o <c>AnoBase</c> do cenário.
    /// Útil para testes e para consultas históricas quando o cenário foi criado para um ano
    /// diferente do ano corrente do servidor.
    /// </para>
    ///
    /// Mapeamento de erros:
    /// <list type="bullet">
    ///   <item>404 — cenário não encontrado (soft-deletado ou Id inexistente).</item>
    ///   <item>400 — ano informado fora do intervalo válido (2020–2050).</item>
    ///   <item>409 — restrição MVP Q9: ano diferente do ano corrente do servidor.</item>
    /// </list>
    ///
    /// SPEC §7.3 (Endpoint Atalho Cenário → Quadro). Fase 3 Task 3.3. AD-11: política Leitura.
    /// </summary>
    /// <param name="id">Id do cenário de simulação.</param>
    /// <param name="ano">
    /// Ano de referência para a projeção. Quando omitido, usa <c>cenario.AnoBase</c>.
    /// </param>
    /// <param name="ct">Token de cancelamento propagado da requisição HTTP.</param>
    [HttpGet("cenarios/{id:guid}/quadro-divida")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<QuadroDividaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetQuadroDividaDoCenario(
        Guid id,
        [FromQuery] int? ano,
        CancellationToken ct)
    {
        try
        {
            // 1. Buscar o cenário para obter AnoBase — lança KeyNotFoundException se não existir.
            CenarioSimulacaoDto cenario = await mediator.Send(
                new GetCenarioSimulacaoByIdQuery(id), ct);

            int anoEfetivo = ano ?? cenario.AnoBase;

            // 2. Delegar ao GetQuadroDividaQuery com ano efetivo e cenarioId.
            //    Task 3.1 adicionou o parâmetro opcional CenarioId; passá-lo aqui faz com
            //    que o projetor de saldo mensal incorpore as captações hipotéticas do cenário
            //    na projeção mês a mês (AD-6, AD-9).
            QuadroDividaDto resultado = await mediator.Send(
                new GetQuadroDividaQuery(anoEfetivo, id), ct);

            return Ok(resultado);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove uma simulação de contratação do cenário (hard delete da filha, cenário permanece).
    /// Bloqueado se cenário estiver Arquivado (409).
    /// SPEC §7.5. AD-11: política Escrita.
    /// </summary>
    [HttpDelete("cenarios/{id:guid}/simulacoes/{simId:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverSimulacao(Guid id, Guid simId, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new RemoverSimulacaoCommand(id, simId), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza todos os campos mutáveis de uma simulação (substituição total, não parcial).
    /// Version é incrementado automaticamente (AD-3) para invalidar cache Redis.
    /// Bloqueado se cenário estiver Arquivado (409).
    /// SPEC §7.5. AD-11: política Escrita.
    /// </summary>
    [HttpPatch("cenarios/{id:guid}/simulacoes/{simId:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CenarioSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarSimulacao(
        Guid id,
        Guid simId,
        [FromBody] AtualizarSimulacaoInput input,
        CancellationToken ct)
    {
        try
        {
            CenarioSimulacaoDto result = await mediator.Send(new AtualizarSimulacaoCommand(id, simId, input), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
