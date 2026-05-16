using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Application.Authorization;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cambio.Commands;
using Sgcf.Application.Cambio.Queries;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/parametros-cotacao")]
public sealed class ParametrosCotacaoController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<ParametroCotacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        IReadOnlyList<ParametroCotacaoDto> result = await mediator.Send(new ListParametrosQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<ParametroCotacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            ParametroCotacaoDto result = await mediator.Send(new GetParametroQuery(id), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ParametroCotacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateParametroCommand command, CancellationToken ct)
    {
        ParametroCotacaoDto result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ParametroCotacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateParametroRequest request, CancellationToken ct)
    {
        try
        {
            UpdateParametroCommand command = new(id, request.TipoCotacao, request.Ativo);
            ParametroCotacaoDto result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("resolve")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<ResolveTipoCotacaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(
        [FromQuery] Guid bancoId,
        [FromQuery] string modalidade,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ModalidadeContrato>(modalidade, true, out ModalidadeContrato modalidadeEnum))
        {
            return BadRequest($"Modalidade inválida: {modalidade}. Valores aceitos: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");
        }

        IReadOnlyList<ParametroCotacao> ativos = await mediator.Send(new ListAtivosParametrosQuery(), ct);
        TipoCotacao tipoCotacao = ResolveTipoCotacaoService.Resolve(ativos, bancoId, modalidadeEnum);
        return Ok(new ResolveTipoCotacaoResponse(tipoCotacao.ToString()));
    }
}

public sealed record UpdateParametroRequest(string TipoCotacao, bool Ativo);

public sealed record ResolveTipoCotacaoResponse(string TipoCotacao);
