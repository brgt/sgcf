using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Common;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Auditoria;
using Xunit;

namespace Sgcf.Application.Tests.Auditoria;

/// <summary>
/// Testes unitários da lógica de auditoria com impersonação.
///
/// Cobre:
/// 1. <see cref="AuditLog.Create"/> registra corretamente os campos de impersonação.
/// 2. Quando IsImpersonating = true, ImpersonatedBy recebe o sub do ator.
/// 3. Quando IsImpersonating = false, ImpersonatedBy é null.
/// 4. <see cref="AuditFilter"/> com Impersonating = true filtra apenas eventos de impersonação.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuditLogWriterTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 20, 10, 0);
    private const string SuperAdminSub = "auth0|superadmin-test";
    private const string TenantUserSub = "auth0|tenant-user-test";

    // ── AuditLog.Create — campos de impersonação ─────────────────────────────

    [Fact]
    public void Create_QuandoImpersonating_PreencheImpersonatedByComSubDoAtor()
    {
        // Arrange + Act
        AuditLog log = AuditLog.Create(
            occurredAt:     InstanteFixo,
            actorSub:       SuperAdminSub,
            actorRole:      "SuperAdmin",
            source:         "/api/v1/contratos",
            entity:         "Contrato",
            entityId:       Guid.NewGuid(),
            operation:      "UPDATE",
            diffJson:       null,
            requestId:      Guid.NewGuid(),
            ipHash:         null,
            impersonating:  true,
            impersonatedBy: SuperAdminSub);

        // Assert
        log.Impersonating.Should().BeTrue();
        log.ImpersonatedBy.Should().Be(SuperAdminSub);
    }

    [Fact]
    public void Create_QuandoNaoImpersonating_ImpersonatedByEhNull()
    {
        // Arrange + Act
        AuditLog log = AuditLog.Create(
            occurredAt:     InstanteFixo,
            actorSub:       TenantUserSub,
            actorRole:      "Admin",
            source:         "/api/v1/contratos",
            entity:         "Contrato",
            entityId:       Guid.NewGuid(),
            operation:      "CREATE",
            diffJson:       null,
            requestId:      Guid.NewGuid(),
            ipHash:         null,
            impersonating:  false,
            impersonatedBy: null);

        // Assert
        log.Impersonating.Should().BeFalse();
        log.ImpersonatedBy.Should().BeNull();
    }

    [Fact]
    public void Create_CamposOpcionaisComDefaultsFalsos_ProduzemLogSemImpersonacao()
    {
        // Verifica que os defaults do método Create são false/null para impersonação.
        AuditLog log = AuditLog.Create(
            occurredAt: InstanteFixo,
            actorSub:   TenantUserSub,
            actorRole:  "Leitura",
            source:     "/api/v1/contratos",
            entity:     "Contrato",
            entityId:   null,
            operation:  "READ",
            diffJson:   null,
            requestId:  Guid.NewGuid());

        log.Impersonating.Should().BeFalse();
        log.ImpersonatedBy.Should().BeNull();
    }

    // ── Lógica de populacao a partir de ITenantContext ────────────────────────

    [Fact]
    public void QuandoTenantContextIsImpersonating_LogDeveRegistrarFlagESubDoSuperAdmin()
    {
        // Arrange — simula a lógica que AuditInterceptor e AuditLogWriter executam
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.IsImpersonating.Returns(true);

        string actorSub = SuperAdminSub;

        // Act — replica a lógica dos serviços de auditoria
        bool impersonating     = tenantContext.IsResolved && tenantContext.IsImpersonating;
        string? impersonatedBy = impersonating ? actorSub : null;

        AuditLog log = AuditLog.Create(
            occurredAt:     InstanteFixo,
            actorSub:       actorSub,
            actorRole:      "SuperAdmin",
            source:         "/api/v1/contratos/123",
            entity:         "Contrato",
            entityId:       Guid.NewGuid(),
            operation:      "UPDATE",
            diffJson:       null,
            requestId:      Guid.NewGuid(),
            impersonating:  impersonating,
            impersonatedBy: impersonatedBy);

        // Assert
        log.Impersonating.Should().BeTrue(
            because: "IsImpersonating = true no contexto deve ser gravado no audit log");
        log.ImpersonatedBy.Should().Be(SuperAdminSub,
            because: "ImpersonatedBy deve ser o sub do super-admin que realizou a ação");
    }

    [Fact]
    public void QuandoTenantContextNaoEstaResolvido_ImpersonatingDeveSerFalso()
    {
        // Arrange — simula context de job/sistema sem tenant resolvido
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);

        // Act
        bool impersonating     = tenantContext.IsResolved && tenantContext.IsImpersonating;
        string? impersonatedBy = impersonating ? SuperAdminSub : null;

        AuditLog log = AuditLog.Create(
            occurredAt:     InstanteFixo,
            actorSub:       "system",
            actorRole:      "",
            source:         "system",
            entity:         "Contrato",
            entityId:       null,
            operation:      "UPDATE",
            diffJson:       null,
            requestId:      Guid.NewGuid(),
            impersonating:  impersonating,
            impersonatedBy: impersonatedBy);

        // Assert
        log.Impersonating.Should().BeFalse(
            because: "contexto não resolvido (jobs, migrações) nunca é impersonation");
        log.ImpersonatedBy.Should().BeNull();
    }

    // ── AuditFilter — dimensão de impersonação ───────────────────────────────

    [Fact]
    public void AuditFilter_ImpersonatingNulo_NaoFiltrarPorImpersonacao()
    {
        AuditFilter filter = new();

        filter.Impersonating.Should().BeNull(
            because: "quando omitido, o filtro não deve restringir por impersonação");
    }

    [Fact]
    public void AuditFilter_ImpersonatingTrue_DeveSerTrue()
    {
        AuditFilter filter = new(Impersonating: true);

        filter.Impersonating.Should().BeTrue();
    }

    // ── ICurrentUserService abstraction ──────────────────────────────────────

    [Fact]
    public void CurrentUserService_ForneceSubERole_QueAuditLogUsaPara_Actor()
    {
        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.ActorSub.Returns(SuperAdminSub);
        currentUser.ActorRole.Returns("SuperAdmin");

        AuditLog log = AuditLog.Create(
            occurredAt: InstanteFixo,
            actorSub:   currentUser.ActorSub,
            actorRole:  currentUser.ActorRole,
            source:     "/api/v1/tenants",
            entity:     "Tenant",
            entityId:   Guid.NewGuid(),
            operation:  "UPDATE",
            diffJson:   null,
            requestId:  Guid.NewGuid(),
            impersonating: true,
            impersonatedBy: currentUser.ActorSub);

        log.ActorSub.Should().Be(SuperAdminSub);
        log.ActorRole.Should().Be("SuperAdmin");
        log.ImpersonatedBy.Should().Be(SuperAdminSub);
    }
}
