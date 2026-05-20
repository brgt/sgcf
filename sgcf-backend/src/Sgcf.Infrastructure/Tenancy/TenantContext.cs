using Sgcf.Application.Tenancy;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Implementação scoped de <see cref="ITenantContext"/>.
/// Mutável apenas via <see cref="Resolve"/> — chamado uma única vez por escopo
/// pelo <c>TenantResolverMiddleware</c>. Depois disso, é imutável.
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    // Armazena o estado resolvido como um único objeto opcional.
    // Evita múltiplos campos nullable que disparariam IDE0032.
    private DadosTenant? _dados;

    /// <inheritdoc />
    public Guid TenantId => _dados?.Id ?? throw new MissingTenantContextException(
        "TenantContext acessado antes da resolução pelo middleware.");

    /// <inheritdoc />
    public string TenantSlug => _dados?.Slug ?? throw new MissingTenantContextException(
        "TenantSlug acessado antes da resolução pelo middleware.");

    /// <inheritdoc />
    public bool IsSuperAdmin => _dados?.IsSuperAdmin ?? false;

    /// <inheritdoc />
    public bool IsImpersonating => _dados?.IsImpersonating ?? false;

    /// <inheritdoc />
    public bool IsResolved => _dados is not null;

    /// <summary>
    /// Resolve o contexto de tenant para este escopo. Deve ser chamado exatamente uma vez
    /// por escopo pelo middleware. Chamadas subsequentes lançam <see cref="InvalidOperationException"/>.
    /// </summary>
    internal void Resolve(Guid tenantId, string slug, bool isSuperAdmin, bool isImpersonating)
    {
        if (_dados is not null)
        {
            throw new InvalidOperationException("TenantContext já resolvido neste scope.");
        }

        _dados = new DadosTenant(tenantId, slug, isSuperAdmin, isImpersonating);
    }

    private sealed record DadosTenant(Guid Id, string Slug, bool IsSuperAdmin, bool IsImpersonating);
}
