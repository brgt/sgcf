using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Tenancy;
using Xunit;

namespace Sgcf.Domain.Tests.Tenancy;

public sealed class TenantTests
{
    private static readonly Guid ValidId = Guid.NewGuid();

    private static IClock Clock
    {
        get
        {
            IClock clock = Substitute.For<IClock>();
            clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 10, 0));
            return clock;
        }
    }

    // ── Criar ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_CriaAtivo()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys S.A.", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        tenant.Status.Should().Be(StatusTenant.Ativo);
        tenant.Slug.Should().Be("proxys");
        tenant.Nome.Should().Be("Proxys S.A.");
        tenant.Id.Should().Be(ValidId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]             // maiúscula
    [InlineData("ab")]            // muito curto (min 3 chars após primeiro)
    [InlineData("-abc")]          // começa com hífen
    [InlineData("abc def")]       // espaço
    [InlineData("abc_def")]       // underscore
    public void Criar_ComSlugInvalido_LancaArgumentException(string slug)
    {
        Action acao = () => Tenant.Criar(ValidId, slug, "Nome", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        acao.Should().Throw<ArgumentException>().WithMessage("*Slug*");
    }

    [Fact]
    public void Criar_ComNomeVazio_LancaArgumentException()
    {
        Action acao = () => Tenant.Criar(ValidId, "proxys", "   ", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        acao.Should().Throw<ArgumentException>().WithMessage("*Nome*");
    }

    [Fact]
    public void Criar_ComCnpjCurto_LancaArgumentException()
    {
        Action acao = () => Tenant.Criar(ValidId, "proxys", "Proxys", "123", PlanoAssinatura.Padrao, Clock);

        acao.Should().Throw<ArgumentException>().WithMessage("*CNPJ*");
    }

    [Fact]
    public void Criar_ComIdVazio_LancaArgumentException()
    {
        Action acao = () => Tenant.Criar(Guid.Empty, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        acao.Should().Throw<ArgumentException>().WithMessage("*Id*");
    }

    [Fact]
    public void Criar_MascaraCnpj_ExibeParcialmente()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "11222333000100", PlanoAssinatura.Padrao, Clock);

        tenant.CnpjMascarado.Should().StartWith("11.");
        tenant.CnpjMascarado.Should().EndWith("-00");
        tenant.CnpjMascarado.Should().Contain("***");
    }

    // ── Suspender ─────────────────────────────────────────────────────────────

    [Fact]
    public void Suspender_TenantAtivo_FicaSuspenso()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        tenant.Suspender("motivo", Clock);

        tenant.Status.Should().Be(StatusTenant.Suspenso);
        tenant.SuspensoEm.Should().NotBeNull();
    }

    [Fact]
    public void Suspender_TenantArquivado_LancaInvalidOperationException()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);
        tenant.Arquivar(Clock);

        Action acao = () => tenant.Suspender("motivo", Clock);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*arquivado*");
    }

    [Fact]
    public void Suspender_TenantJaSuspenso_EIdempotente()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);
        tenant.Suspender("motivo", Clock);
        Instant suspensaoOriginal = tenant.SuspensoEm!.Value;

        tenant.Suspender("segundo motivo", Clock);

        tenant.SuspensoEm!.Value.Should().Be(suspensaoOriginal);
    }

    // ── Reativar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Reativar_TenantSuspenso_FicaAtivo()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);
        tenant.Suspender("motivo", Clock);

        tenant.Reativar(Clock);

        tenant.Status.Should().Be(StatusTenant.Ativo);
        tenant.SuspensoEm.Should().BeNull();
    }

    [Fact]
    public void Reativar_TenantArquivado_LancaInvalidOperationException()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);
        tenant.Arquivar(Clock);

        Action acao = () => tenant.Reativar(Clock);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*arquivado*");
    }

    // ── Arquivar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Arquivar_TenantAtivo_FicaArquivado()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);

        tenant.Arquivar(Clock);

        tenant.Status.Should().Be(StatusTenant.Arquivado);
        tenant.ArquivadoEm.Should().NotBeNull();
    }

    [Fact]
    public void Arquivar_JaArquivado_EIdempotente()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Padrao, Clock);
        tenant.Arquivar(Clock);
        Instant arquivadoOriginal = tenant.ArquivadoEm!.Value;

        tenant.Arquivar(Clock);

        tenant.ArquivadoEm!.Value.Should().Be(arquivadoOriginal);
    }

    // ── AtualizarPlano ────────────────────────────────────────────────────────

    [Fact]
    public void AtualizarPlano_AlteraPlano()
    {
        Tenant tenant = Tenant.Criar(ValidId, "proxys", "Proxys", "00.000.000/0001-00", PlanoAssinatura.Trial, Clock);

        tenant.AtualizarPlano(PlanoAssinatura.Enterprise, Clock);

        tenant.Plano.Should().Be(PlanoAssinatura.Enterprise);
    }
}
