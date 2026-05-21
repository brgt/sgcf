namespace Sgcf.Application.Eventos;

/// <summary>
/// In-process broadcast bus for real-time domain events.
/// Implementations must be singleton.
/// </summary>
public interface IEventoBus
{
    /// <summary>Broadcasts an event to all connected SSE subscribers.</summary>
    public void Broadcast(EventoSistemaDto evento);

    /// <summary>
    /// Subscribes to the event stream. Returns a channel reader that receives
    /// all events published after this call. The caller must dispose the
    /// subscription when done (e.g. when the HTTP connection is closed).
    /// </summary>
    public EventoBusSubscription Subscribe();
}
