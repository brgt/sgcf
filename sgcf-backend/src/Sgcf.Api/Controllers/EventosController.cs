using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sgcf.Api.Serialization;
using Sgcf.Application.Authorization;
using Sgcf.Application.Eventos;

namespace Sgcf.Api.Controllers;

[ApiController]
[Route("api/v1/eventos")]
public sealed class EventosController(IEventoBus bus) : ControllerBase
{
    // Dedicated options for SSE serialization — must include InstantJsonConverter
    // to match the project-wide NodaTime serialization convention (no external package).
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new InstantJsonConverter(), new JsonStringEnumConverter() }
    };

    /// <summary>
    /// SSE stream. Client connects and receives real-time events as <c>data: {json}\n\n</c> frames.
    /// </summary>
    [HttpGet("stream")]
    [Authorize(Policy = Policies.Leitura)]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        using EventoBusSubscription subscription = bus.Subscribe();

        await foreach (EventoSistemaDto evento in
            subscription.Reader.ReadAllAsync(cancellationToken))
        {
            string json = JsonSerializer.Serialize(evento, JsonOpts);
            string frame = $"data: {json}\n\n";
            await Response.WriteAsync(frame, Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Publishes a manual test event. Useful for FE development and integration tests.
    /// Restricted to admin roles — not for regular users.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public IActionResult Publicar([FromBody] EventoSistemaDto evento)
    {
        bus.Broadcast(evento);
        return Accepted();
    }
}
