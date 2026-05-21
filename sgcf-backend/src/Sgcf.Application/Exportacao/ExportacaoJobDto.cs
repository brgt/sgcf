using NodaTime;
using Sgcf.Domain.Exportacao;

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
    Instant? ConcluidoEm)
{
    public static ExportacaoJobDto From(ExportacaoJob j) =>
        new(j.Id, j.Tipo.ToString(), j.Status.ToString(),
            j.ParametrosJson, j.ResultadoJson, j.MensagemErro,
            j.SolicitadoPor, j.CriadoEm, j.IniciadoEm, j.ConcluidoEm);
}
