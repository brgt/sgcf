using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Alertas;
using Sgcf.Application.Alertas.Rules;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Alertas;

/// <summary>
/// Testes unitários para <see cref="RegraLimiteBancoUtilizacao"/>.
/// Verifica os limiares de alerta (85% = Atenção, 95% = Crítico) e a ausência
/// de alertas quando a utilização é inferior ao limiar.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegraLimiteBancoUtilizacaoTests
{
    private static readonly Instant AgoraFixa = Instant.FromUtc(2026, 5, 21, 9, 0);
    private static readonly LocalDate Hoje = new(2026, 5, 21);

    private readonly IClock _clock;
    private readonly ILimiteBancoRepository _limiteBancoRepo;
    private readonly IAlertaRepository _alertaRepo;
    private readonly RegraLimiteBancoUtilizacao _sut;

    public RegraLimiteBancoUtilizacaoTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.GetCurrentInstant().Returns(AgoraFixa);

        _limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        _alertaRepo = Substitute.For<IAlertaRepository>();

        _sut = new RegraLimiteBancoUtilizacao(_limiteBancoRepo, _alertaRepo, _clock);
    }

    [Fact]
    public void Nome_RetornaLimiteBanco()
    {
        _sut.Nome.Should().Be("limite-banco");
    }

    [Theory]
    [InlineData(0.85, SeveridadeAlerta.Atencao, "limiar exato de 85% deve gerar Atenção")]
    [InlineData(0.90, SeveridadeAlerta.Atencao, "90% deve gerar Atenção")]
    [InlineData(0.94, SeveridadeAlerta.Atencao, "94% deve gerar Atenção (abaixo do limiar crítico)")]
    public async Task AvaliarAsync_UtilizacaoAcimaDe85Porcento_CriaAlertaAtencao(
        decimal percentualUtilizado, SeveridadeAlerta severidadeEsperada, string porque)
    {
        // Arrange
        LimiteBanco limite = CriarLimiteComUtilizacao(1_000_000m, percentualUtilizado);

        _limiteBancoRepo
            .ListAsync(null, null, Arg.Any<CancellationToken>())
            .Returns([limite]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        alertaSalvo.Should().NotBeNull();
        alertaSalvo!.Severidade.Should().Be(severidadeEsperada, because: porque);
        alertaSalvo.Categoria.Should().Be(CategoriaAlerta.LimiteBanco);
        alertaSalvo.OrigemId.Should().Be(limite.Id);
        alertaSalvo.ChaveIdempotencia.Should().Be($"limite-banco:{limite.Id}:{Hoje:yyyy-MM-dd}");
    }

    [Theory]
    [InlineData(0.95, SeveridadeAlerta.Critico, "limiar exato de 95% deve gerar Crítico")]
    [InlineData(1.00, SeveridadeAlerta.Critico, "100% utilizado deve gerar Crítico")]
    public async Task AvaliarAsync_UtilizacaoAcimaDe95Porcento_CriaAlertaCritico(
        decimal percentualUtilizado, SeveridadeAlerta severidadeEsperada, string porque)
    {
        // Arrange
        LimiteBanco limite = CriarLimiteComUtilizacao(1_000_000m, percentualUtilizado);

        _limiteBancoRepo
            .ListAsync(null, null, Arg.Any<CancellationToken>())
            .Returns([limite]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        alertaSalvo!.Severidade.Should().Be(severidadeEsperada, because: porque);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(0.50)]
    [InlineData(0.84)]
    public async Task AvaliarAsync_UtilizacaoAbaixoDe85Porcento_NaoCriaAlerta(decimal percentualUtilizado)
    {
        // Arrange — utilização abaixo do limiar de 85% não deve gerar alerta
        LimiteBanco limite = CriarLimiteComUtilizacao(1_000_000m, percentualUtilizado);

        _limiteBancoRepo
            .ListAsync(null, null, Arg.Any<CancellationToken>())
            .Returns([limite]);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        await _alertaRepo.DidNotReceive().TryAddIdempotentAsync(
            Arg.Any<Alerta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AvaliarAsync_SemLimitesCadastrados_NaoCriaAlertas()
    {
        // Arrange
        _limiteBancoRepo
            .ListAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<LimiteBanco>());

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        await _alertaRepo.DidNotReceive().TryAddIdempotentAsync(
            Arg.Any<Alerta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AvaliarAsync_PerfisVisiveis_IncluesGerenteFinanceiroECfo()
    {
        // Arrange — 90% de utilização: acima do limiar
        LimiteBanco limite = CriarLimiteComUtilizacao(1_000_000m, 0.90m);

        _limiteBancoRepo
            .ListAsync(null, null, Arg.Any<CancellationToken>())
            .Returns([limite]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        IEnumerable<PerfilCockpit> perfis = alertaSalvo!.PerfisVisiveis.Select(p => p.Perfil);
        perfis.Should().Contain(PerfilCockpit.GerenteFinanceiro);
        perfis.Should().Contain(PerfilCockpit.Cfo);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um <see cref="LimiteBanco"/> com valor limite = <paramref name="valorLimite"/>
    /// e valor utilizado = valorLimite * <paramref name="percentualUtilizado"/>.
    /// </summary>
    private LimiteBanco CriarLimiteComUtilizacao(decimal valorLimite, decimal percentualUtilizado)
    {
        Money limiteBrl = new(valorLimite, Moeda.Brl);
        LocalDate vigencia = new(2026, 1, 1);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: limiteBrl,
            dataVigenciaInicio: vigencia,
            clock: _clock);

        decimal valorUsado = Math.Round(valorLimite * percentualUtilizado, 6, MidpointRounding.AwayFromZero);
        if (valorUsado > 0m)
        {
            limite.RegistrarUso(new Money(valorUsado, Moeda.Brl), _clock);
        }

        return limite;
    }
}
