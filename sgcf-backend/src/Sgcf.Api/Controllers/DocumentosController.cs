using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Filters;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Application.Documentos;
using Sgcf.Application.Documentos.Commands;
using Sgcf.Application.Documentos.Queries;
using Sgcf.Domain.Documentos;

namespace Sgcf.Api.Controllers;

public sealed record CreateDocumentoRequest(
    TipoDocumento Tipo,
    string Nome,
    string? DataEmissao,
    string? DataVencimento,
    string? UrlArmazenamento,
    string? Observacao);

public sealed record UpdateDocumentoRequest(
    string Nome,
    string? DataEmissao,
    string? DataVencimento,
    string? UrlArmazenamento,
    string? Observacao);

public sealed record AtualizarStatusDocumentoRequest(
    StatusDocumento NovoStatus,
    string? Observacao);

[ApiController]
[Route("api/v1/contratos/{contratoId:guid}/documentos")]
public sealed class DocumentosController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesEnvelope]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid contratoId, CancellationToken cancellationToken)
    {
        EnvelopeResponse<IReadOnlyList<DocumentoContratualDto>> result =
            await sender.Send(new GetDocumentosContratoQuery(contratoId), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<DocumentoContratualDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid contratoId,
        [FromBody] CreateDocumentoRequest body,
        CancellationToken cancellationToken)
    {
        NodaTime.LocalDate? dataEmissao = ParseLocalDate(body.DataEmissao);
        NodaTime.LocalDate? dataVencimento = ParseLocalDate(body.DataVencimento);

        EnvelopeResponse<DocumentoContratualDto> result = await sender.Send(
            new CreateDocumentoContratualCommand(
                contratoId,
                body.Tipo,
                body.Nome,
                dataEmissao,
                dataVencimento,
                body.UrlArmazenamento,
                body.Observacao),
            cancellationToken);

        return CreatedAtAction(nameof(List), new { contratoId }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<DocumentoContratualDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid contratoId,
        Guid id,
        [FromBody] UpdateDocumentoRequest body,
        CancellationToken cancellationToken)
    {
        NodaTime.LocalDate? dataEmissao = ParseLocalDate(body.DataEmissao);
        NodaTime.LocalDate? dataVencimento = ParseLocalDate(body.DataVencimento);

        EnvelopeResponse<DocumentoContratualDto> result = await sender.Send(
            new UpdateDocumentoContratualCommand(
                id,
                contratoId,
                body.Nome,
                dataEmissao,
                dataVencimento,
                body.UrlArmazenamento,
                body.Observacao),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid contratoId, Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDocumentoContratualCommand(id, contratoId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<EnvelopeResponse<DocumentoContratualDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarStatus(
        Guid contratoId,
        Guid id,
        [FromBody] AtualizarStatusDocumentoRequest body,
        CancellationToken cancellationToken)
    {
        EnvelopeResponse<DocumentoContratualDto> result = await sender.Send(
            new AtualizarStatusDocumentoCommand(id, contratoId, body.NovoStatus, body.Observacao),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Converte string no formato <c>yyyy-MM-dd</c> para <see cref="NodaTime.LocalDate"/>.
    /// Retorna <c>null</c> quando a string é nula ou vazia — datas opcionais são tratadas
    /// como ausentes em vez de gerar erro.
    /// </summary>
    private static NodaTime.LocalDate? ParseLocalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        NodaTime.Text.ParseResult<NodaTime.LocalDate> result =
            NodaTime.Text.LocalDatePattern.Iso.Parse(value);

        if (!result.Success)
        {
            throw new ArgumentException($"Data '{value}' inválida. Use o formato yyyy-MM-dd.");
        }

        return result.Value;
    }
}
