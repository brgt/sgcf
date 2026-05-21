using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Painel.EconomiaTributaria;

namespace Sgcf.Api.Controllers;

/// <summary>
/// Endpoint de leitura para economia tributária acumulada (benefício estimado IRPJ + CSLL).
/// GAP-CKP-21.
/// </summary>
[ApiController]
[Route("api/v1/painel")]
public sealed class EconomiaTributariaController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retorna a economia tributária acumulada para o período informado.
    ///
    /// <para>
    /// Calcula o benefício fiscal estimado derivado da economia de juros negociada
    /// (equalizada por CDI), aplicando a alíquota efetiva combinada de IRPJ + CSLL (34%).
    /// Inclui subtotais por banco credor quando o campo <c>BancoId</c> está disponível
    /// no snapshot da proposta aceita.
    /// </para>
    /// </summary>
    /// <param name="deAno">Ano inicial do período (ex: 2025).</param>
    /// <param name="deMes">Mês inicial do período (1–12).</param>
    /// <param name="ateAno">Ano final do período (ex: 2025).</param>
    /// <param name="ateMes">Mês final do período (1–12).</param>
    /// <param name="bancoId">Filtro opcional por banco credor.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    [HttpGet("economia-tributaria")]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<EconomiaTributariaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int deAno,
        [FromQuery] int deMes,
        [FromQuery] int ateAno,
        [FromQuery] int ateMes,
        [FromQuery] Guid? bancoId,
        CancellationToken cancellationToken)
    {
        EnvelopeResponse<EconomiaTributariaDto> resultado = await mediator.Send(
            new GetEconomiaTributariaQuery(deAno, deMes, ateAno, ateMes, bancoId),
            cancellationToken);

        return Ok(resultado);
    }
}
