using NodaTime;
using Sgcf.Domain.Conformidade;

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
    Instant AtualizadoEm)
{
    public static RegistroRegulatorioDto From(RegistroRegulatorio r) =>
        new(r.Id, r.ContratoId, r.Tipo.ToString(), r.Status.ToString(),
            r.NumeroRegistro, r.DataRegistro, r.DataVencimento,
            r.Observacao, r.CriadoEm, r.AtualizadoEm);
}
