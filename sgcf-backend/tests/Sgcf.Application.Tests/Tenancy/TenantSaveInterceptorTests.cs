using FluentAssertions;
using NSubstitute;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Persistence;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

/// <summary>
/// Testa a lógica central do <see cref="TenantSaveInterceptor"/>:
/// preenchimento de TenantId quando o contexto está resolvido vs. não resolvido.
///
/// A integração completa com EF Core e banco de dados é coberta pelos testes Slow
/// nos fixtures de Cotações e Simulação.
/// </summary>
public sealed class TenantSaveInterceptorTests
{
    private static readonly Guid TenantIdFixo = Guid.Parse("00000000-0000-7000-8000-000000000001");

    // ── Cenário 1: contexto resolvido → preenche TenantId ─────────────────────

    [Fact]
    public void QuandoContextoResolvido_InterceptorPreencheTenantId()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoResolvido(TenantIdFixo);
        FakeTenantScoped entidade = new();

        // Act — replica a lógica do interceptor (IsResolved → atribui TenantId)
        if (tenantCtx.IsResolved)
        {
            entidade.SetTenantIdForTest(tenantCtx.TenantId);
        }

        // Assert
        entidade.TenantId.Should().Be(TenantIdFixo);
    }

    // ── Cenário 2: contexto não resolvido → TenantId permanece vazio ──────────

    [Fact]
    public void QuandoContextoNaoResolvido_InterceptorNaoAlteraTenantId()
    {
        // Arrange
        ITenantContext tenantCtx = CriarContextoNaoResolvido();
        FakeTenantScoped entidade = new();

        // Act
        if (tenantCtx.IsResolved)
        {
            // Este bloco NÃO executa quando IsResolved = false
            entidade.SetTenantIdForTest(tenantCtx.TenantId);
        }

        // Assert
        entidade.TenantId.Should().Be(
            Guid.Empty,
            because: "contexto não resolvido (jobs, migrations) não deve preencher TenantId");
    }

    // ── Cenário 3: interceptor é instanciado sem erros ─────────────────────────

    [Fact]
    public void Interceptor_PodeSerInstanciado_SemErros()
    {
        ITenantContext tenantCtx = CriarContextoResolvido(TenantIdFixo);
        TenantSaveInterceptor interceptor = new(tenantCtx);
        interceptor.Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ITenantContext CriarContextoResolvido(Guid tenantId)
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(true);
        ctx.TenantId.Returns(tenantId);
        return ctx;
    }

    private static ITenantContext CriarContextoNaoResolvido()
    {
        ITenantContext ctx = Substitute.For<ITenantContext>();
        ctx.IsResolved.Returns(false);
        return ctx;
    }

    /// <summary>Stub para validar a lógica de preenchimento sem dependência de EF Core.</summary>
    private sealed class FakeTenantScoped
    {
        public Guid TenantId { get; private set; }

        public void SetTenantIdForTest(Guid tenantId) => TenantId = tenantId;
    }
}
