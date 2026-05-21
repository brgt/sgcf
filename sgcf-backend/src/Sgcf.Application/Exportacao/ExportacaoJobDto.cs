using NodaTime;

namespace Sgcf.Application.Exportacao;

public sealed record ExportacaoJobDto(
    Guid Id,
    string Tipo,
    string Status,
    string? ParametrosJson,
    string? ResultadoJson,
    string? MensagemErro,
    string SolicitadoPor,
    Instant CriadoEm,
    Instant? IniciadoEm,
    Instant? ConcluidoEm);
