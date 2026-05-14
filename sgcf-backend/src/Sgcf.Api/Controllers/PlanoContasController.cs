using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contabilidade.Commands;
using Sgcf.Application.Contabilidade.Queries;
using System.Collections.Generic;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/plano-contas")]
public sealed class PlanoContasController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<PlanoContasDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        CancellationToken ct)
    {
        IReadOnlyList<PlanoContasDto> result = await mediator.Send(new ListContasQuery(search, ativo), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            PlanoContasDto result = await mediator.Send(new GetContaQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContaCommand command, CancellationToken ct)
    {
        PlanoContasDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<PlanoContasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarContaRequest request, CancellationToken ct)
    {
        try
        {
            AtualizarContaCommand command = new(id, request.Nome, request.Natureza, request.CodigoSapB1);
            PlanoContasDto result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{contaId:guid}/lancamentos")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<LancamentoContabilDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLancamento(
        Guid contaId,
        [FromBody] CreateLancamentoContabilRequest request,
        CancellationToken ct)
    {
        CreateLancamentoContabilCommand command = new(
            contaId,
            request.Data,
            request.Origem,
            request.ValorDecimal,
            request.Moeda,
            request.Descricao);

        LancamentoContabilDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetLancamentosByConta), new { contaId }, result);
    }

    [HttpGet("{contaId:guid}/lancamentos")]
    [Authorize(Policy = Policies.Auditoria)]
    [ProducesResponseType<IReadOnlyList<LancamentoContabilDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLancamentosByConta(Guid contaId, CancellationToken ct)
    {
        IReadOnlyList<LancamentoContabilDto> result =
            await mediator.Send(new GetLancamentosByContaQuery(contaId), ct);
        return Ok(result);
    }
}

public sealed record AtualizarContaRequest(
    string Nome,
    string Natureza,
    string? CodigoSapB1);

public sealed record CreateLancamentoContabilRequest(
    DateOnly Data,
    string Origem,
    decimal ValorDecimal,
    string Moeda,
    string Descricao);
