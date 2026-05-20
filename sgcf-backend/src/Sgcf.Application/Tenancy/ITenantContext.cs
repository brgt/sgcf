namespace Sgcf.Application.Tenancy;

/// <summary>
/// Contexto de tenant resolvido pelo middleware para o escopo da requisição corrente.
/// Disponível para injeção em handlers, serviços e filtros na camada Application.
/// </summary>
public interface ITenantContext
{
    /// <summary>ID único do tenant resolvido. Lança <see cref="MissingTenantContextException"/> se não resolvido.</summary>
    public Guid TenantId { get; }

    /// <summary>Slug kebab-case do tenant. Lança <see cref="MissingTenantContextException"/> se não resolvido.</summary>
    public string TenantSlug { get; }

    /// <summary>Indica se o usuário corrente é super-admin Nordware (cross-tenant).</summary>
    public bool IsSuperAdmin { get; }

    /// <summary>Indica se o super-admin está atuando no tenant via impersonation (header X-Tenant-Id).</summary>
    public bool IsImpersonating { get; }

    /// <summary>Retorna true se o contexto foi resolvido pelo middleware para este escopo.</summary>
    public bool IsResolved { get; }
}
