using NodaTime;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy;

public sealed record TenantDto(
    Guid Id,
    string Slug,
    string Nome,
    string CnpjMascarado,
    string Status,
    string Plano,
    Instant CriadoEm,
    Instant? SuspensoEm,
    Instant? ArquivadoEm)
{
    public static TenantDto From(Tenant t) => new(
        t.Id,
        t.Slug,
        t.Nome,
        t.CnpjMascarado,
        t.Status.ToString(),
        t.Plano.ToString(),
        t.CriadoEm,
        t.SuspensoEm,
        t.ArquivadoEm);
}
