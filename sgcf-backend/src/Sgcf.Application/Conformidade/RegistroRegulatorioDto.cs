using NodaTime;

namespace Sgcf.Application.Conformidade;

public sealed record RegistroRegulatorioDto(
    Guid Id,
    Guid ContratoId,
    string Tipo,
    string Status,
    string? NumeroRegistro,
    LocalDate? DataRegistro,
    LocalDate? DataVencimento,
    string? Observacao,
    Instant CriadoEm,
    Instant AtualizadoEm);
