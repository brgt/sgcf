using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Sistema;
using Sgcf.Application.Tenancy;
using Sgcf.Application.Tenancy.Services;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Sistema;
using Sgcf.Domain.Tenancy;
using Xunit;

namespace Sgcf.Application.Tests.Tenancy;

/// <summary>
/// Testes unitários do <see cref="TenantProvisioner"/>.
/// Usa NSubstitute para todos os repositórios — sem container Docker.
/// </summary>
public sealed class TenantProvisionerTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 20, 10, 0);
    private static readonly Guid TenantIdFixo = Guid.Parse("00000000-0000-7000-8000-000000000001");

    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IParametroSistemaRepository _parametroSistemaRepo = Substitute.For<IParametroSistemaRepository>();
    private readonly IParametroCotacaoRepository _parametroCotacaoRepo = Substitute.For<IParametroCotacaoRepository>();
    private readonly IPlanoContasRepository _planoContasRepo = Substitute.For<IPlanoContasRepository>();
    private readonly IPlanoContasModeloRepository _planoContasModeloRepo = Substitute.For<IPlanoContasModeloRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly TenantProvisioner _sut;

    public TenantProvisionerTests()
    {
        _clock.GetCurrentInstant().Returns(InstanteFixo);

        // Por padrão, PlanoContas vazio (tenant não provisionado) e modelo com 3 entradas.
        IReadOnlyList<PlanoContasGerencial> contasVazias = [];
        IReadOnlyList<PlanoContasModelo> modeloPadrao = CriarModeloPadrao();
        _planoContasRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(contasVazias);
        _planoContasModeloRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(modeloPadrao);

        _sut = new TenantProvisioner(
            _tenantRepo,
            _parametroSistemaRepo,
            _parametroCotacaoRepo,
            _planoContasRepo,
            _planoContasModeloRepo,
            _tenantContext,
            _clock,
            NullLogger<TenantProvisioner>.Instance);
    }

    // ── Cenário 1: provisionamento com tudo novo ─────────────────────────────

    /// <summary>
    /// Quando não existe nenhum dado para o tenant, todos os seeds são executados
    /// e o resultado indica criados > 0.
    /// </summary>
    [Fact]
    public async Task ProvisionarAsync_TenantAtivo_SemDadosPrevios_CriaParametros()
    {
        // Arrange
        Tenant tenant = CriarTenantAtivo();
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(tenant);
        _parametroSistemaRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(false);
        _parametroCotacaoRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        ResultadoProvisionamento resultado = await _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert
        resultado.TenantId.Should().Be(TenantIdFixo);
        resultado.TenantSlug.Should().Be("tenant-teste");
        resultado.Criados["parametrosSistema"].Should().Be(1);
        resultado.Criados["parametrosCotacao"].Should().Be(1);
        resultado.Criados["planoContas"].Should().Be(3, because: "o modelo padrão tem 3 entradas");
        resultado.Ignorados["parametrosSistema"].Should().Be(0);
        resultado.Ignorados["parametrosCotacao"].Should().Be(0);
        resultado.Ignorados["planoContas"].Should().Be(0);
        resultado.ProvisionadoEm.Should().Be(InstanteFixo);

        _parametroSistemaRepo.Received(1).Add(Arg.Any<ParametroSistema>());
        _parametroCotacaoRepo.Received(1).Add(Arg.Any<ParametroCotacao>());
        _planoContasRepo.Received(3).Add(Arg.Any<PlanoContasGerencial>());
        await _parametroSistemaRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Cenário 2: idempotência — dados já existem ───────────────────────────

    /// <summary>
    /// Quando todos os dados já existem, nenhum registro é criado.
    /// O resultado deve indicar ignorados > 0 e criados = 0 para cada categoria.
    /// </summary>
    [Fact]
    public async Task ProvisionarAsync_TenantAtivo_ComDadosExistentes_NaoCriaDuplicatas()
    {
        // Arrange
        Tenant tenant = CriarTenantAtivo();
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(tenant);
        _parametroSistemaRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(true);
        _parametroCotacaoRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(true);

        // PlanoContas já existe para este tenant.
        IReadOnlyList<PlanoContasGerencial> contasExistentes = CriarContasExistentes();
        _planoContasRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(contasExistentes);

        // Act
        ResultadoProvisionamento resultado = await _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert
        resultado.Criados["parametrosSistema"].Should().Be(0);
        resultado.Criados["parametrosCotacao"].Should().Be(0);
        resultado.Criados["planoContas"].Should().Be(0);
        resultado.Ignorados["parametrosSistema"].Should().Be(1);
        resultado.Ignorados["parametrosCotacao"].Should().Be(1);

        _parametroSistemaRepo.DidNotReceive().Add(Arg.Any<ParametroSistema>());
        _parametroCotacaoRepo.DidNotReceive().Add(Arg.Any<ParametroCotacao>());
        _planoContasRepo.DidNotReceive().Add(Arg.Any<PlanoContasGerencial>());
        // SaveChanges ainda é chamado — flush de qualquer mudança pendente no contexto.
        await _parametroSistemaRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Cenário 3: tenant inexistente → KeyNotFoundException ─────────────────

    [Fact]
    public async Task ProvisionarAsync_TenantInexistente_LancaKeyNotFoundException()
    {
        // Arrange
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        // Act
        Func<Task> act = () => _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{TenantIdFixo}*");
    }

    // ── Cenário 4: tenant arquivado → TenantArquivadoException ───────────────

    [Fact]
    public async Task ProvisionarAsync_TenantArquivado_LancaInvalidOperationException()
    {
        // Arrange
        Tenant tenant = CriarTenantArquivado();
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(tenant);

        // Act
        Func<Task> act = () => _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*arquivado*");
    }

    // ── Cenário 5: tenant suspenso → TenantSuspendoException ─────────────────

    [Fact]
    public async Task ProvisionarAsync_TenantSuspenso_LancaInvalidOperationException()
    {
        // Arrange
        Tenant tenant = CriarTenantSuspenso();
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(tenant);

        // Act
        Func<Task> act = () => _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*suspenso*");
    }

    // ── Cenário 6: plano_contas clonado do modelo ─────────────────────────────

    [Fact]
    public async Task ProvisionarAsync_PlanoContas_ClonaModeloParaTenant()
    {
        // Arrange
        Tenant tenant = CriarTenantAtivo();
        _tenantRepo.GetAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(tenant);
        _parametroSistemaRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(false);
        _parametroCotacaoRepo.ExisteParaTenantAsync(TenantIdFixo, Arg.Any<CancellationToken>()).Returns(false);
        // _planoContasRepo.ListAllAsync retorna vazio por padrão (configurado no construtor)

        // Act
        ResultadoProvisionamento resultado = await _sut.ProvisionarAsync(TenantIdFixo, CancellationToken.None);

        // Assert — 3 entradas do modelo são clonadas
        resultado.Criados["planoContas"].Should().Be(3,
            because: "cada entrada do modelo gera uma conta no tenant");
        _planoContasRepo.Received(3).Add(Arg.Any<PlanoContasGerencial>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PlanoContasModelo[] CriarModeloPadrao()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 10, 0));

        return
        [
            PlanoContasModelo.Criar("1.1.1", "Conta Corrente em BRL",       NaturezaConta.Ativo,     null, clock),
            PlanoContasModelo.Criar("2.1.1", "FINIMP em Moeda Estrangeira", NaturezaConta.Passivo,   null, clock),
            PlanoContasModelo.Criar("3.1.1", "Rendimento de CDB Cativo",    NaturezaConta.Resultado, null, clock)
        ];
    }

    private static PlanoContasGerencial[] CriarContasExistentes()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 10, 0));

        return [PlanoContasGerencial.Criar("1.1.1", "Conta Corrente em BRL", NaturezaConta.Ativo, clock)];
    }

    private static Tenant CriarTenantAtivo()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return Tenant.Criar(TenantIdFixo, "tenant-teste", "Empresa Teste", "12345678000195", PlanoAssinatura.Padrao, clock);
    }

    private static Tenant CriarTenantArquivado()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        Tenant tenant = Tenant.Criar(TenantIdFixo, "tenant-teste", "Empresa Teste", "12345678000195", PlanoAssinatura.Padrao, clock);
        tenant.Arquivar(clock);
        return tenant;
    }

    private static Tenant CriarTenantSuspenso()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        Tenant tenant = Tenant.Criar(TenantIdFixo, "tenant-teste", "Empresa Teste", "12345678000195", PlanoAssinatura.Padrao, clock);
        tenant.Suspender("motivo-teste", clock);
        return tenant;
    }
}
