using FluentAssertions;
using NodaTime;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Sgcf.Application.Alertas;
using Sgcf.Application.Alertas.Rules;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;
using Xunit;

namespace Sgcf.Application.Tests.Alertas;

/// <summary>
/// Testes unitários para <see cref="RegraVencimentoIminente"/>.
/// Repositórios são mockados via NSubstitute — sem banco de dados.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegraVencimentoIminenteTests
{
    // Clock fixado: 2026-05-21T09:00Z = 2026-05-21 em BRT (UTC-3)
    private static readonly Instant AgoraFixa = Instant.FromUtc(2026, 5, 21, 9, 0);
    private static readonly LocalDate Hoje = new(2026, 5, 21);

    private readonly IClock _clock;
    private readonly IEventoCronogramaRepository _cronogramaRepo;
    private readonly IAlertaRepository _alertaRepo;
    private readonly RegraVencimentoIminente _sut;

    public RegraVencimentoIminenteTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.GetCurrentInstant().Returns(AgoraFixa);

        _cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        _alertaRepo = Substitute.For<IAlertaRepository>();

        // Por padrão retorna lista vazia para todos os horizontes.
        _cronogramaRepo
            .ListPendentesVencendoEmAsync(Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EventoCronograma>());

        _sut = new RegraVencimentoIminente(_cronogramaRepo, _alertaRepo, _clock);
    }

    [Fact]
    public void Nome_RetornaVencimento()
    {
        _sut.Nome.Should().Be("vencimento");
    }

    [Fact]
    public async Task AvaliarAsync_ParcelaVencendoHoje_CriaAlertaCritico()
    {
        // Arrange — evento vencendo hoje (D-0)
        LocalDate vencimento = Hoje; // D-0
        EventoCronograma evento = CriarEvento(vencimento);

        _cronogramaRepo
            .ListPendentesVencendoEmAsync(Hoje, Arg.Any<CancellationToken>())
            .Returns([evento]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        alertaSalvo.Should().NotBeNull();
        alertaSalvo!.Severidade.Should().Be(SeveridadeAlerta.Critico,
            because: "D-0 deve gerar alerta Crítico");
        alertaSalvo.Categoria.Should().Be(CategoriaAlerta.Vencimento);
        alertaSalvo.OrigemId.Should().Be(evento.Id);
        alertaSalvo.ChaveIdempotencia.Should().Be($"vencimento:{evento.Id}:{Hoje:yyyy-MM-dd}");
    }

    [Fact]
    public async Task AvaliarAsync_ParcelaVencendoEm3Dias_CriaAlertaAtencao()
    {
        // Arrange — evento vencendo em D+3
        LocalDate vencimentoEm3 = Hoje.PlusDays(3);
        EventoCronograma evento = CriarEvento(vencimentoEm3);

        _cronogramaRepo
            .ListPendentesVencendoEmAsync(vencimentoEm3, Arg.Any<CancellationToken>())
            .Returns([evento]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        alertaSalvo.Should().NotBeNull();
        alertaSalvo!.Severidade.Should().Be(SeveridadeAlerta.Atencao,
            because: "D-3 deve gerar alerta Atenção");
    }

    [Fact]
    public async Task AvaliarAsync_ParcelaVencendoEm7Dias_CriaAlertaInformativo()
    {
        // Arrange — evento vencendo em D+7
        LocalDate vencimentoEm7 = Hoje.PlusDays(7);
        EventoCronograma evento = CriarEvento(vencimentoEm7);

        _cronogramaRepo
            .ListPendentesVencendoEmAsync(vencimentoEm7, Arg.Any<CancellationToken>())
            .Returns([evento]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        alertaSalvo.Should().NotBeNull();
        alertaSalvo!.Severidade.Should().Be(SeveridadeAlerta.Informativo,
            because: "D-7 deve gerar alerta Informativo");
    }

    [Fact]
    public async Task AvaliarAsync_ChaveJaExiste_NaoDuplicaAlerta()
    {
        // Arrange — simula chave de idempotência já registrada (TryAddIdempotentAsync retorna false)
        EventoCronograma evento = CriarEvento(Hoje);

        _cronogramaRepo
            .ListPendentesVencendoEmAsync(Hoje, Arg.Any<CancellationToken>())
            .Returns([evento]);

        _alertaRepo
            .TryAddIdempotentAsync(Arg.Any<Alerta>(), Arg.Any<CancellationToken>())
            .Returns(false); // alerta já existe

        // Act — chamado duas vezes no mesmo dia
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert — TryAddIdempotentAsync foi chamado 2 vezes (1 por D-0 por cada chamada),
        // mas a idempotência é responsabilidade do repositório — a regra sempre tenta adicionar.
        await _alertaRepo.Received(2).TryAddIdempotentAsync(
            Arg.Any<Alerta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AvaliarAsync_SemEventosPendentes_NaoCriaAlertas()
    {
        // Arrange — todos os horizontes retornam lista vazia (configurado no construtor)

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        await _alertaRepo.DidNotReceive().TryAddIdempotentAsync(
            Arg.Any<Alerta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AvaliarAsync_PerfisVisiveis_IncluesTesourariaEGerenteFinanceiro()
    {
        // Arrange
        EventoCronograma evento = CriarEvento(Hoje);

        _cronogramaRepo
            .ListPendentesVencendoEmAsync(Hoje, Arg.Any<CancellationToken>())
            .Returns([evento]);

        Alerta? alertaSalvo = null;
        _alertaRepo
            .TryAddIdempotentAsync(Arg.Do<Alerta>(a => alertaSalvo = a), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.AvaliarAsync(Hoje, CancellationToken.None);

        // Assert
        IEnumerable<PerfilCockpit> perfis = alertaSalvo!.PerfisVisiveis.Select(p => p.Perfil);
        perfis.Should().Contain(PerfilCockpit.Tesouraria);
        perfis.Should().Contain(PerfilCockpit.GerenteFinanceiro);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EventoCronograma CriarEvento(LocalDate dataPrevista)
        => EventoCronograma.Criar(
            contratoId: Guid.NewGuid(),
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: dataPrevista,
            valorMoedaOriginal: new Money(100_000m, Moeda.Brl));
}
