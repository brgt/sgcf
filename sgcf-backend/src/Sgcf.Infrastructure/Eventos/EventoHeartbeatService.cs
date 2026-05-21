using Microsoft.Extensions.Hosting;
using NodaTime;
using Sgcf.Application.Eventos;

namespace Sgcf.Infrastructure.Eventos;

internal sealed class EventoHeartbeatService(IEventoBus bus, IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            bus.Broadcast(new EventoSistemaDto(
                Tipo: "heartbeat",
                EntidadeTipo: null,
                EntidadeId: null,
                Mensagem: null,
                OcorridoEm: clock.GetCurrentInstant()));
        }
    }
}
