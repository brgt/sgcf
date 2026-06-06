using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Queries;

namespace Sgcf.Api.Controllers;

// ── Corpos de requisição ──────────────────────────────────────────────────────

/// <summary>
/// Corpo para adicionar banco-alvo a uma cotação.
/// Os campos opcionais são usados para comparação com o pré-preenchimento automático
/// e geração de alertas de coerência (Task 4.2).
/// </summary>
public sealed record AdicionarBancoRequest(
    Guid BancoId,
    bool PreencherGarantiaAutomaticamente = true,
    string? GarantiaExigidaManual = null,
    decimal? ValorGarantiaExigidaBrlManual = null,
    bool? GarantiaEhCdbCativoManual = null,
    decimal? RendimentoCdbAaPercentual = null);

/// <summary>Corpo para cancelar cotação com motivo obrigatório.</summary>
public sealed record CancelarCotacaoRequest(string Motivo);

/// <summary>Corpo para atualizar campos básicos editáveis de uma cotação.</summary>
/// <remarks>S40: prefira o tenor { PrazoMaximoValor, PrazoMaximoUnidade }; PrazoMaximoDias é legado.</remarks>
public sealed record AtualizarCotacaoRequest(
    int? PrazoMaximoDias,
    string? Observacoes,
    int? PrazoMaximoValor = null,
    string? PrazoMaximoUnidade = null);

// ── Controller ───────────────────────────────────────────────────────────────

/// <summary>
/// Gerencia cotações de captação financeira (propostas, comparativo, auditoria).
/// Cobre o ciclo completo: Rascunho → EmCaptacao → Comparada → Aceita → Convertida.
/// SPEC §7.
///
/// Erros são traduzidos centralmente pelo GlobalExceptionHandler (RFC 7807): KeyNotFoundException → 404,
/// ConflitoDeEstadoException/InvalidOperationException → 409, ValidationException → 400. SPEC S40 §5.
/// </summary>
[ApiController]
[Route("api/v1/cotacoes")]
public sealed class CotacoesController(IMediator mediator) : ControllerBase
{
    // ── Cotação ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria nova cotação em status Rascunho. Exige PTAX D-1 cadastrada para modalidades cambiais.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CotacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarCotacaoCommand command,
        CancellationToken cancellationToken)
    {
        CotacaoDto result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Lista cotações com paginação e filtros opcionais por status, modalidade e período.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<PagedResult<CotacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? modalidade,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        PagedResult<CotacaoDto> result = await mediator.Send(
            new ListCotacoesQuery(page, pageSize, status, modalidade, desde, ate),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retorna detalhe completo de uma cotação, incluindo todas as propostas.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<CotacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        CotacaoDto result = await mediator.Send(new GetCotacaoQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza campos editáveis da cotação (apenas em Rascunho).
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CotacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarCotacaoRequest body,
        CancellationToken cancellationToken)
    {
        CotacaoDto result = await mediator.Send(
            new AtualizarCotacaoCommand(
                id, body.PrazoMaximoDias, body.Observacoes,
                body.PrazoMaximoValor, body.PrazoMaximoUnidade),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Cancela cotação (soft delete). Equivale a mover para status Recusada com motivo.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarViaDeletion(
        Guid id,
        [FromBody] CancelarCotacaoRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelarCotacaoCommand(id, body.Motivo), cancellationToken);
        return NoContent();
    }

    // ── Bancos-alvo ───────────────────────────────────────────────────────────

    /// <summary>
    /// Adiciona banco-alvo à cotação. Valida limite disponível do banco.
    /// Retorna 200 com template de garantia pré-preenchido quando o limite possui garantias
    /// exigidas e <c>preencherGarantiaAutomaticamente = true</c>.
    /// Retorna 409 se limite insuficiente, banco já incluso ou CDB cativo sem rendimento.
    /// </summary>
    [HttpPost("{id:guid}/bancos")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<AdicionarBancoNaCotacaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdicionarBanco(
        Guid id,
        [FromBody] AdicionarBancoRequest body,
        CancellationToken cancellationToken)
    {
        AdicionarBancoNaCotacaoResponse resultado = await mediator.Send(
            new AdicionarBancoNaCotacaoCommand(
                CotacaoId: id,
                BancoId: body.BancoId,
                PreencherGarantiaAutomaticamente: body.PreencherGarantiaAutomaticamente,
                GarantiaExigidaManual: body.GarantiaExigidaManual,
                ValorGarantiaExigidaBrlManual: body.ValorGarantiaExigidaBrlManual,
                GarantiaEhCdbCativoManual: body.GarantiaEhCdbCativoManual,
                RendimentoCdbAaPercentual: body.RendimentoCdbAaPercentual),
            cancellationToken);

        // Retorna 200 com body quando há dados de garantia pré-preenchida ou alertas;
        // caso contrário 204 para manter compatibilidade com os callers existentes.
        return resultado.Proposta is not null || resultado.Alertas.Count > 0
            ? Ok(resultado)
            : NoContent();
    }

    /// <summary>
    /// Remove banco-alvo da cotação (apenas em Rascunho ou EmCaptacao).
    /// </summary>
    [HttpDelete("{id:guid}/bancos/{bancoId:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoverBanco(
        Guid id,
        Guid bancoId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoverBancoDaCotacaoCommand(id, bancoId), cancellationToken);
        return NoContent();
    }

    // ── Transições de estado ─────────────────────────────────────────────────

    /// <summary>
    /// Envia cotação aos bancos: Rascunho → EmCaptacao.
    /// </summary>
    [HttpPost("{id:guid}/enviar")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new EnviarCotacaoCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Encerra captação de propostas: EmCaptacao → Comparada.
    /// </summary>
    [HttpPost("{id:guid}/encerrar-captacao")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EncerrarCaptacao(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new EncerrarCaptacaoCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cancela cotação com motivo explícito. Move para status Recusada.
    /// </summary>
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(
        Guid id,
        [FromBody] CancelarCotacaoRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelarCotacaoCommand(id, body.Motivo), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Re-busca PTAX atual e invalida cache de CET de todas as propostas.
    /// Disponível em EmCaptacao ou Comparada.
    /// </summary>
    [HttpPost("{id:guid}/refresh-mercado")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<CotacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RefreshMercado(Guid id, CancellationToken cancellationToken)
    {
        CotacaoDto result = await mediator.Send(new RefreshCotacaoMercadoCommand(id), cancellationToken);
        return Ok(result);
    }

    // ── Relatórios ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tabela comparativa de propostas com três métricas: taxa nominal, CET e custo total equivalente em BRL.
    /// SPEC §5.3.
    /// </summary>
    [HttpGet("{id:guid}/comparativo")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<ComparativoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Comparativo(
        Guid id,
        [FromQuery] decimal? aliquotaIrrfPercentual,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ComparativoDto> result = await mediator.Send(
            new CompararPropostasQuery(id, aliquotaIrrfPercentual), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Trilha de auditoria da cotação: transições de estado, atores, timestamps. SPEC §4.2.
    /// </summary>
    [HttpGet("{id:guid}/auditoria")]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<IReadOnlyList<Sgcf.Application.Auditoria.AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Auditoria(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<Sgcf.Application.Auditoria.AuditLogDto> result =
            await mediator.Send(new GetCotacaoAuditTrailQuery(id), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Relatório de economia negociada agregada por período.
    /// Parâmetros: <c>de</c> e <c>ate</c> no formato YYYY-MM; <c>bancoId</c> opcional.
    /// SPEC §5.2.
    /// </summary>
    [HttpGet("economia")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EconomiaPeriodoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Economia(
        [FromQuery] string de,
        [FromQuery] string ate,
        [FromQuery] Guid? bancoId,
        CancellationToken cancellationToken)
    {
        if (!TryParseYearMonth(de, out NodaTime.YearMonth ymDe))
        {
            return BadRequest(new { error = $"Parâmetro 'de' inválido: '{de}'. Use formato YYYY-MM." });
        }

        if (!TryParseYearMonth(ate, out NodaTime.YearMonth ymAte))
        {
            return BadRequest(new { error = $"Parâmetro 'ate' inválido: '{ate}'. Use formato YYYY-MM." });
        }

        EconomiaPeriodoDto result = await mediator.Send(
            new GetEconomiaPeriodoQuery(ymDe, ymAte, bancoId),
            cancellationToken);

        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converte string "YYYY-MM" em <see cref="YearMonth"/>. NodaTime não expõe TryParse direto para esse formato.
    /// </summary>
    private static bool TryParseYearMonth(string input, out YearMonth result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input) || input.Length != 7 || input[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(input.AsSpan(0, 4), out int year) ||
            !int.TryParse(input.AsSpan(5, 2), out int month))
        {
            return false;
        }

        if (year < 1 || month < 1 || month > 12)
        {
            return false;
        }

        result = new YearMonth(year, month);
        return true;
    }

    // ── Propostas ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registra proposta recebida do banco. Calcula CET automaticamente. SPEC §5.1, §6.1.
    /// </summary>
    [HttpPost("{id:guid}/propostas")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<PropostaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarProposta(
        Guid id,
        [FromBody] RegistrarPropostaCommand command,
        CancellationToken cancellationToken)
    {
        RegistrarPropostaCommand cmd = command with { CotacaoId = id };

        PropostaDto result = await mediator.Send(cmd, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    /// <summary>
    /// Atualiza proposta existente (apenas em status Recebida). Recalcula CET. SPEC §6.1.
    /// </summary>
    [HttpPatch("{id:guid}/propostas/{propostaId:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<PropostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarProposta(
        Guid id,
        Guid propostaId,
        [FromBody] AtualizarPropostaCommand command,
        CancellationToken cancellationToken)
    {
        AtualizarPropostaCommand cmd = command with { CotacaoId = id, PropostaId = propostaId };

        PropostaDto result = await mediator.Send(cmd, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Aceita proposta. Registra <c>AceitaPor</c> do operador autenticado. SPEC §3.2 regra 4, §4.1.
    /// </summary>
    [HttpPost("{id:guid}/propostas/{propostaId:guid}/aceitar")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AceitarProposta(
        Guid id,
        Guid propostaId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AceitarPropostaCommand(id, propostaId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reverte aceitação de proposta: Aceita → Comparada. Apenas se cotação ainda não convertida.
    /// O <paramref name="propostaId"/> é ignorado pelo comando — a cotação só pode ter uma proposta aceita.
    /// SPEC §4.1.
    /// </summary>
    [HttpPost("{id:guid}/propostas/{propostaId:guid}/desfazer-aceitacao")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DesfazerAceitacao(
        Guid id,
        Guid propostaId,
        CancellationToken cancellationToken)
    {
        // propostaId é ignorado: a cotação possui exatamente uma proposta aceita (SPEC §3.2 regra 4)
        _ = propostaId;

        await mediator.Send(new DesfazerAceitacaoCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Converte cotação aceita em contrato. Cria Contrato + EconomiaNegociacao atomicamente.
    /// SPEC §4.1, §5.2.
    /// </summary>
    [HttpPost("{id:guid}/converter-em-contrato")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConverterEmContrato(
        Guid id,
        [FromBody] ConverterEmContratoCommand command,
        CancellationToken cancellationToken)
    {
        ConverterEmContratoCommand cmd = command with { CotacaoId = id };

        ContratoDto result = await mediator.Send(cmd, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
