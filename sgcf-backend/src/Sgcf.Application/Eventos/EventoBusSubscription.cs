using System.Threading.Channels;

namespace Sgcf.Application.Eventos;

/// <summary>
/// Represents a single SSE client's subscription. Dispose to unsubscribe.
/// </summary>
public sealed class EventoBusSubscription(
    ChannelReader<EventoSistemaDto> reader,
    Action onDispose) : IDisposable
{
    public ChannelReader<EventoSistemaDto> Reader { get; } = reader;

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        onDispose();
    }
}
