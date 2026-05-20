using FluentAssertions;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Tenancy;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void IsResolved_AntesDeResolve_RetornaFalse()
    {
        TenantContext sut = new();

        sut.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void TenantId_AntesDeResolve_LancaMissingTenantContextException()
    {
        TenantContext sut = new();

        Action acao = () => _ = sut.TenantId;

        acao.Should().Throw<MissingTenantContextException>();
    }

    [Fact]
    public void TenantSlug_AntesDeResolve_LancaMissingTenantContextException()
    {
        TenantContext sut = new();

        Action acao = () => _ = sut.TenantSlug;

        acao.Should().Throw<MissingTenantContextException>();
    }

    [Fact]
    public void IsResolved_AposResolve_RetornaTrue()
    {
        TenantContext sut = new();
        Guid tenantId = Guid.NewGuid();

        sut.Resolve(tenantId, "meu-tenant", isSuperAdmin: false, isImpersonating: false);

        sut.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void TenantId_AposResolve_RetornaIdCorreto()
    {
        TenantContext sut = new();
        Guid tenantId = Guid.NewGuid();

        sut.Resolve(tenantId, "meu-tenant", isSuperAdmin: false, isImpersonating: false);

        sut.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantSlug_AposResolve_RetornaSlugCorreto()
    {
        TenantContext sut = new();

        sut.Resolve(Guid.NewGuid(), "proxys", isSuperAdmin: false, isImpersonating: false);

        sut.TenantSlug.Should().Be("proxys");
    }

    [Fact]
    public void IsSuperAdmin_AposResolveComTrue_RetornaTrue()
    {
        TenantContext sut = new();

        sut.Resolve(Guid.NewGuid(), "nordware", isSuperAdmin: true, isImpersonating: false);

        sut.IsSuperAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsImpersonating_AposResolveComTrue_RetornaTrue()
    {
        TenantContext sut = new();

        sut.Resolve(Guid.NewGuid(), "proxys", isSuperAdmin: true, isImpersonating: true);

        sut.IsImpersonating.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Duplicado_LancaInvalidOperationException()
    {
        TenantContext sut = new();
        sut.Resolve(Guid.NewGuid(), "meu-tenant", isSuperAdmin: false, isImpersonating: false);

        Action segundoResolve = () => sut.Resolve(Guid.NewGuid(), "outro-tenant", isSuperAdmin: false, isImpersonating: false);

        segundoResolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*já resolvido*");
    }
}
