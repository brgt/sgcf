namespace Sgcf.Api.IntegrationTests.CrossTenantIsolation.Fixtures;

/// <summary>
/// Identidades fixas dos dois tenants usados em toda a suite de isolamento.
///
/// TenantA corresponde ao tenant Proxys de desenvolvimento (mesmo ID que ProxysDevTenant)
/// para reutilizar o seed existente.  TenantB é um tenant ACME provisionado pelo fixture.
/// </summary>
internal static class TenantTestData
{
    /// <summary>Tenant A — Proxys Comércio Eletrônico (alinhado com ProxysDevTenant.Id).</summary>
    public static readonly Guid TenantAId   = new("00000000-0000-7000-8000-000000000001");
    public const string TenantASlug         = "proxys";
    public const string TenantANome         = "Proxys Comércio Eletrônico";
    public const string TenantACnpj         = "00000000000100";

    /// <summary>Tenant B — ACME S/A, criado dinamicamente pelo fixture.</summary>
    public static readonly Guid TenantBId   = new("00000000-0000-7000-8000-000000000002");
    public const string TenantBSlug         = "acme-cross-tenant";
    public const string TenantBNome         = "ACME Cross-Tenant S/A";
    public const string TenantBCnpj         = "00000000000200";
}
