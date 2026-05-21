using NodaTime;

namespace Sgcf.Application.Eventos;

public sealed record EventoSistemaDto(
    string Tipo,          // e.g. "alerta.criado", "covenant.violado", "heartbeat"
    string? EntidadeTipo, // e.g. "Alerta", "Covenant", null for heartbeat
    Guid? EntidadeId,
    string? Mensagem,
    Instant OcorridoEm);
