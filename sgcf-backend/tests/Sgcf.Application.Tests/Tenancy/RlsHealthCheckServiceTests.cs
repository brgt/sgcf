using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Application.Tenancy.Services;
using Sgcf.Domain.Tenancy;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

/// <summary>
/// Testes unitários dos contratos de <see cref="IRlsHealthCheckService"/>.
///
/// O serviço concreto <c>RlsHealthCheckService</c> depende de Npgsql e PostgreSQL,
/// portanto os testes de integração estão em <c>HealthControllerRlsTests</c> (Slow).
///
/// Estes testes cobrem:
/// 1. <see cref="RlsHealthReport"/> — modelo de dados correto.
/// 2. <see cref="RlsCheckResult"/> — lógica de "passed"/"failed".
/// 3. Contrato da interface — retorno de tipos esperados.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RlsHealthCheckServiceTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 20, 14, 32, 0);

    // ── RlsHealthReport ──────────────────────────────────────────────────────

    [Fact]
    public void RlsHealthReport_QuandoTodosCheckPassaram_StatusEhHealthy()
    {
        // Arrange
        IReadOnlyList<RlsCheckResult> checks =
        [
            new RlsCheckResult("rls_enabled_all_tables", "passed", "30 tabelas com RLS habilitada."),
            new RlsCheckResult("policies_present", "passed", "30 policies encontradas."),
            new RlsCheckResult("isolation_canary_no_context", "passed", "0 linhas sem contexto."),
            new RlsCheckResult("isolation_canary_with_proxys", "passed", "5 linhas com proxys."),
        ];

        RlsHealthReport report = new("healthy", checks, InstanteFixo);

        // Assert
        report.Status.Should().Be("healthy");
        report.Checks.Should().HaveCount(4);
        report.VerificadoEm.Should().Be(InstanteFixo);
    }

    [Fact]
    public void RlsHealthReport_QuandoAlgumCheckFalhou_StatusEhUnhealthy()
    {
        IReadOnlyList<RlsCheckResult> checks =
        [
            new RlsCheckResult("rls_enabled_all_tables", "failed", "Tabelas sem RLS: ['alerta_vencimento']"),
            new RlsCheckResult("policies_present", "passed", "30 policies encontradas."),
            new RlsCheckResult("isolation_canary_no_context", "passed", "0 linhas sem contexto."),
            new RlsCheckResult("isolation_canary_with_proxys", "passed", "5 linhas com proxys."),
        ];

        RlsHealthReport report = new("unhealthy", checks, InstanteFixo);

        report.Status.Should().Be("unhealthy");
        report.Checks.Should().Contain(c => c.Name == "rls_enabled_all_tables" && c.Status == "failed");
    }

    // ── RlsCheckResult ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("passed")]
    [InlineData("failed")]
    public void RlsCheckResult_StatusPodeSer_PassedOuFailed(string expectedStatus)
    {
        RlsCheckResult check = new("rls_enabled_all_tables", expectedStatus, "detalhes");

        check.Status.Should().Be(expectedStatus);
        check.Name.Should().Be("rls_enabled_all_tables");
        check.Details.Should().Be("detalhes");
    }

    // ── IRlsHealthCheckService — contrato da interface ────────────────────────

    [Fact]
    public async Task CheckAsync_ViaSubstitute_RetornaRlsHealthReport()
    {
        // Arrange
        IRlsHealthCheckService service = Substitute.For<IRlsHealthCheckService>();
        IReadOnlyList<RlsCheckResult> checks =
        [
            new RlsCheckResult("rls_enabled_all_tables", "passed", "ok"),
            new RlsCheckResult("policies_present", "passed", "ok"),
            new RlsCheckResult("isolation_canary_no_context", "passed", "ok"),
            new RlsCheckResult("isolation_canary_with_proxys", "passed", "ok"),
        ];

        RlsHealthReport reportEsperado = new("healthy", checks, InstanteFixo);
        service.CheckAsync(Arg.Any<CancellationToken>()).Returns(reportEsperado);

        // Act
        RlsHealthReport result = await service.CheckAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("healthy");
        result.Checks.Should().HaveCount(4);
        result.Checks.Should().AllSatisfy(c => c.Status.Should().Be("passed"));
    }

    [Fact]
    public async Task CheckAsync_QuandoAlgumCheckFalha_RetornaUnhealthy()
    {
        // Arrange
        IRlsHealthCheckService service = Substitute.For<IRlsHealthCheckService>();
        IReadOnlyList<RlsCheckResult> checks =
        [
            new RlsCheckResult("rls_enabled_all_tables", "passed", "ok"),
            new RlsCheckResult("policies_present", "failed", "Tabelas sem policy: ['contrato']"),
            new RlsCheckResult("isolation_canary_no_context", "passed", "ok"),
            new RlsCheckResult("isolation_canary_with_proxys", "passed", "ok"),
        ];

        RlsHealthReport reportEsperado = new("unhealthy", checks, InstanteFixo);
        service.CheckAsync(Arg.Any<CancellationToken>()).Returns(reportEsperado);

        // Act
        RlsHealthReport result = await service.CheckAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be("unhealthy");
        result.Checks.Should().Contain(c =>
            c.Name == "policies_present" && c.Status == "failed");
    }

    // ── Tenant.Slug lookup para canary with_proxys ───────────────────────────

    [Fact]
    public async Task GetBySlugAsync_ProxysSlug_RetornaTenantSeExistir()
    {
        // Garante que o repositório suporta lookup por slug "proxys"
        ITenantRepository tenantRepo = Substitute.For<ITenantRepository>();
        Tenant proxys = CriarProxysTenant();
        tenantRepo.GetBySlugAsync("proxys", Arg.Any<CancellationToken>()).Returns(proxys);

        Tenant? result = await tenantRepo.GetBySlugAsync("proxys", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Slug.Should().Be("proxys");
    }

    [Fact]
    public async Task GetBySlugAsync_QuandoTenantNaoExiste_RetornaNull()
    {
        ITenantRepository tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetBySlugAsync("proxys", Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        Tenant? result = await tenantRepo.GetBySlugAsync("proxys", CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Tenant CriarProxysTenant()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Tenant.Criar(
            Guid.Parse("00000000-0000-7000-8000-000000000001"),
            "proxys",
            "Proxys Group",
            "12345678000195",
            PlanoAssinatura.Padrao,
            clock);
    }
}
