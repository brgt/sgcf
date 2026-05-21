using System.Collections.Concurrent;
using System.Threading.Channels;
using Sgcf.Application.Eventos;

namespace Sgcf.Infrastructure.Eventos;

/// <summary>
/// In-process fan-out event bus backed by unbounded channels.
/// Each call to Subscribe() creates a dedicated channel for one SSE client.
/// Broadcast() writes to all active channels.
/// </summary>
internal sealed class InMemoryEventoBus : IEventoBus
{
    private readonly ConcurrentDictionary<Guid, Channel<EventoSistemaDto>> _subscribers = new();

    public void Broadcast(EventoSistemaDto evento)
    {
        foreach (var (_, channel) in _subscribers)
        {
            channel.Writer.TryWrite(evento);
        }
    }

    public EventoBusSubscription Subscribe()
    {
        Guid id = Guid.NewGuid();
        Channel<EventoSistemaDto> channel = Channel.CreateUnbounded<EventoSistemaDto>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });
        _subscribers[id] = channel;

        return new EventoBusSubscription(channel.Reader, () => _subscribers.TryRemove(id, out _));
    }
}
