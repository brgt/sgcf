using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Sgcf.Application.Authorization;
using Sgcf.Api.Export;
using Sgcf.Api.Filters;
using Sgcf.Application.Antecipacao;
using Sgcf.Application.Antecipacao.Commands;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Commands;
using Sgcf.Application.Contratos.Queries;
using Sgcf.Application.Hedge;
using Sgcf.Application.Hedge.Commands;
using Sgcf.Application.Hedge.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Api.Controllers;

public sealed record AddHedgeRequest(
    string Tipo,
    Guid ContraparteId,
    decimal NotionalMoedaOriginal,
    string MoedaBase,
    DateOnly DataContratacao,
    DateOnly DataVencimento,
    decimal? StrikeForward,
    decimal? StrikePut,
    decimal? StrikeCall);

public sealed record SimularAntecipacaoRequest(
    string TipoAntecipacao,
    DateOnly DataEfetiva,
    decimal? ValorPrincipalAQuitarMoedaOriginal,
    decimal? TaxaMercadoAtualAa,
    decimal? IndenizacaoBancoMoedaOriginal,
    bool SalvarSimulacao = true);

public sealed record GerarCronogramaRequest(
    decimal? AliqIrrfPct,
    decimal? AliqIofCambioPct,
    decimal? TarifaRofBrl,
    decimal? TarifaCadempBrl,
    decimal? TaxaFgiAaPct = null);

/// <summary>
/// Corpo da requisição para adição de garantia. Mapeia diretamente para <see cref="AddGarantiaCommand"/>.
/// Exatamente um dos payloads de tipo deve ser preenchido conforme o valor de <c>Tipo</c>.
/// </summary>
public sealed record AddGarantiaRequest(
    string Tipo,
    decimal ValorBrl,
    DateOnly DataConstituicao,
    DateOnly? DataLiberacaoPrevista,
    string? Observacoes,
    AddGarantiaCdbPayload? Cdb = null,
    AddGarantiaSblcPayload? Sblc = null,
    AddGarantiaAvalPayload? Aval = null,
    AddGarantiaAlienacaoPayload? Alienacao = null,
    AddGarantiaDuplicatasPayload? Duplicatas = null,
    AddGarantiaRecebiveisPayload? Recebiveis = null,
    AddGarantiaBoletoPayload? Boleto = null,
    AddGarantiaFgiPayload? FgiDetail = null);

[ApiController]
[Route("api/v1/contratos")]
public sealed class ContratosController(IMediator mediator, IExportacaoAuditLog auditLog) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<PagedResult<ContratoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] Guid? bancoId,
        [FromQuery] string? modalidade,
        [FromQuery] string? moeda,
        [FromQuery] string? status,
        [FromQuery] string? vencDe,
        [FromQuery] string? vencAte,
        [FromQuery] decimal? valorMin,
        [FromQuery] decimal? valorMax,
        [FromQuery] bool? temHedge,
        [FromQuery] bool? temGarantia,
        [FromQuery] bool? temAlerta,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sort = "DataVencimento",
        [FromQuery] string dir = "asc",
        CancellationToken cancellationToken = default)
    {
        LocalDate? vencDeDate = null;
        LocalDate? vencAteDate = null;

        if (!string.IsNullOrEmpty(vencDe) && DateOnly.TryParse(vencDe, out DateOnly d1))
        {
            vencDeDate = LocalDate.FromDateOnly(d1);
        }

        if (!string.IsNullOrEmpty(vencAte) && DateOnly.TryParse(vencAte, out DateOnly d2))
        {
            vencAteDate = LocalDate.FromDateOnly(d2);
        }

        ModalidadeContrato? modalidadeEnum = null;
        if (!string.IsNullOrEmpty(modalidade) && Enum.TryParse<ModalidadeContrato>(modalidade, ignoreCase: true, out ModalidadeContrato modalidadeParsed))
        {
            modalidadeEnum = modalidadeParsed;
        }

        Moeda? moedaEnum = null;
        if (!string.IsNullOrEmpty(moeda) && Enum.TryParse<Moeda>(moeda, ignoreCase: true, out Moeda moedaParsed))
        {
            moedaEnum = moedaParsed;
        }

        StatusContrato? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StatusContrato>(status, ignoreCase: true, out StatusContrato statusParsed))
        {
            statusEnum = statusParsed;
        }

        ContratoFilter filter = new(q, bancoId, modalidadeEnum, moedaEnum, statusEnum, vencDeDate, vencAteDate, valorMin, valorMax, temHedge, temGarantia, temAlerta, page, pageSize, sort, dir);
        PagedResult<ContratoDto> result = await mediator.Send(new ListContratosQuery(filter), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            ContratoDto result = await mediator.Send(new GetContratoQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.Escrita)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContratoCommand command, CancellationToken cancellationToken)
    {
        ContratoDto result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/tabela-completa")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<TabelaCompletaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTabelaCompleta(
        Guid id,
        [FromQuery] DateOnly? dataReferencia,
        [FromQuery] decimal? cotacao,
        [FromQuery] string? formato,
        CancellationToken cancellationToken)
    {
        try
        {
            LocalDate? localDataRef = dataReferencia.HasValue
                ? LocalDate.FromDateTime(dataReferencia.Value.ToDateTime(TimeOnly.MinValue))
                : (LocalDate?)null;

            TabelaCompletaDto result = await mediator.Send(
                new GetTabelaCompletaQuery(id, localDataRef, cotacao), cancellationToken);

            string formatoNormalizado = (formato ?? "json").ToLowerInvariant();

            if (formatoNormalizado == "pdf")
            {
                string usuario = User.Identity?.Name ?? "system";
                string dataHora = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

                byte[] pdfBytes = TabelaCompletaPdfExporter.Gerar(result, usuario, dataHora);

                await auditLog.RegistrarAsync(id, "pdf", usuario, cancellationToken);

                return File(pdfBytes, "application/pdf", "tabela-completa.pdf");
            }

            if (formatoNormalizado == "xlsx")
            {
                string usuario = User.Identity?.Name ?? "system";

                byte[] xlsxBytes = TabelaCompletaExcelExporter.Gerar(result);

                await auditLog.RegistrarAsync(id, "xlsx", usuario, cancellationToken);

                return File(
                    xlsxBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "tabela-completa.xlsx");
            }

            if (formatoNormalizado != "json")
            {
                return BadRequest(new { detail = $"Formato '{formato}' não suportado. Use: json, pdf ou xlsx." });
            }

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }


    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteContratoCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/gerar-cronograma")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<IReadOnlyList<EventoCronogramaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GerarCronograma(Guid id, [FromBody] GerarCronogramaRequest body, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<EventoCronogramaDto> result = await mediator.Send(
                new GerarCronogramaCommand(id, body.AliqIrrfPct, body.AliqIofCambioPct, body.TarifaRofBrl, body.TarifaCadempBrl, TaxaFgiAaPct: body.TaxaFgiAaPct),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/simular-antecipacao")]
    [Authorize(Policy = Policies.Leitura)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<ResultadoSimulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SimularAntecipacao(
        Guid id,
        [FromBody] SimularAntecipacaoRequest body,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TipoAntecipacao>(body.TipoAntecipacao, ignoreCase: true, out TipoAntecipacao tipoAntecipacao))
        {
            return BadRequest(new { detail = $"TipoAntecipacao inválido: '{body.TipoAntecipacao}'." });
        }

        LocalDate dataEfetiva = LocalDate.FromDateTime(body.DataEfetiva.ToDateTime(TimeOnly.MinValue));

        try
        {
            ResultadoSimulacaoDto result = await mediator.Send(
                new SimularAntecipacaoCommand(
                    ContratoId: id,
                    TipoAntecipacao: tipoAntecipacao,
                    DataEfetiva: dataEfetiva,
                    ValorPrincipalAQuitarMoedaOriginal: body.ValorPrincipalAQuitarMoedaOriginal,
                    TaxaMercadoAtualAa: body.TaxaMercadoAtualAa,
                    IndenizacaoBancoMoedaOriginal: body.IndenizacaoBancoMoedaOriginal,
                    SalvarSimulacao: body.SalvarSimulacao,
                    CreatedBy: User.Identity?.Name ?? "system",
                    Source: "API"),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }

    [HttpGet("{id:guid}/garantias")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<GarantiaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListGarantias(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<GarantiaDto> result = await mediator.Send(new ListGarantiasQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/garantias/indicadores")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IndicadoresGarantiaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIndicadoresGarantia(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            IndicadoresGarantiaDto result = await mediator.Send(
                new GetIndicadoresGarantiaQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/garantias")]
    [Authorize(Policy = Policies.Escrita)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType<GarantiaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddGarantia(
        Guid id,
        [FromBody] AddGarantiaRequest body,
        CancellationToken cancellationToken)
    {
        AddGarantiaCommand command = new(
            ContratoId: id,
            Tipo: body.Tipo,
            ValorBrl: body.ValorBrl,
            DataConstituicao: body.DataConstituicao,
            DataLiberacaoPrevista: body.DataLiberacaoPrevista,
            Observacoes: body.Observacoes,
            CreatedBy: User.Identity?.Name ?? "system",
            Cdb: body.Cdb,
            Sblc: body.Sblc,
            Aval: body.Aval,
            Alienacao: body.Alienacao,
            Duplicatas: body.Duplicatas,
            Recebiveis: body.Recebiveis,
            Boleto: body.Boleto,
            FgiDetail: body.FgiDetail);

        try
        {
            GarantiaDto result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(ListGarantias), new { id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/garantias/{garantiaId:guid}")]
    [Authorize(Policy = Policies.Gerencial)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelarGarantia(
        Guid id,
        Guid garantiaId,
        CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new CancelarGarantiaCommand(id, garantiaId), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/hedges")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<HedgeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddHedge(
        Guid id,
        [FromBody] AddHedgeRequest body,
        CancellationToken cancellationToken)
    {
        AddHedgeCommand command = new(
            ContratoId: id,
            Tipo: body.Tipo,
            ContraparteId: body.ContraparteId,
            NotionalMoedaOriginal: body.NotionalMoedaOriginal,
            MoedaBase: body.MoedaBase,
            DataContratacao: new LocalDate(body.DataContratacao.Year, body.DataContratacao.Month, body.DataContratacao.Day),
            DataVencimento: new LocalDate(body.DataVencimento.Year, body.DataVencimento.Month, body.DataVencimento.Day),
            StrikeForward: body.StrikeForward,
            StrikePut: body.StrikePut,
            StrikeCall: body.StrikeCall);

        try
        {
            HedgeDto result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(ListHedges), new { id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }

    [HttpGet("{id:guid}/hedges")]
    [Authorize(Policy = Policies.Leitura)]
    [ProducesResponseType<IReadOnlyList<HedgeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListHedges(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<HedgeDto> result = await mediator.Send(new ListHedgesQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/importar-cronograma")]
    [Authorize(Policy = Policies.Escrita)]
    [ProducesResponseType<IReadOnlyList<EventoCronogramaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ImportarCronograma(
        Guid id,
        [FromBody] IReadOnlyList<ParcelaManualRequest> parcelas,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<EventoCronogramaDto> result = await mediator.Send(
                new ImportarCronogramaCommand(id, parcelas),
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
