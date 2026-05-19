using System.Text.Json;

using FluentAssertions;

using MediatR;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Domain.Simulacao;
using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

[Trait("Category", "Mcp")]
public sealed class SimulacaoToolsTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static SimulacaoTools CriarTools(IMediator mediator) => new(mediator);

    private static QuadroDividaDto CriarQuadroDividaDto(Guid? cenarioId = null) =>
        new(
            Ano: 2026,
            DataReferencia: new DateOnly(2026, 5, 19),
            SnapshotInicial: new SaldoPorBancoAtualDto(
                Bancos: new List<SaldoBancoAtualDto>().AsReadOnly(),
                SaldoTotalBrl: 0m,
                DataReferencia: NodaTime.LocalDate.FromDateTime(DateTime.UtcNow)),
            Projecao: new QuadroDividaProjecaoDto(
                Meses: new List<MesProjecaoDto>().AsReadOnly()),
            Sumario: new QuadroDividaSumarioDto(
                SaldoTotalInicioAno: 10_000_000m,
                SaldoTotalFimAno: 8_000_000m,
                TotalAmortizacaoNoAno: 2_000_000m,
                TotalCaptacaoNoAno: 0m,
                VariacaoAnualPercentual: -20m),
            Alertas: new List<string>().AsReadOnly(),
            CenarioAplicado: cenarioId.HasValue
                ? new CenarioAplicadoDto(cenarioId.Value, "Realista 2026", "Ativo", 2026, 3)
                : null);

    private static CenarioSimulacaoResumoDto CriarResumoDto(
        Guid? id = null,
        string status = "Ativo",
        int anoBase = 2026) =>
        new(
            Id: id ?? Guid.NewGuid(),
            Nome: "Cenário Teste",
            Status: status,
            AnoBase: anoBase,
            QtdeSimulacoes: 2,
            CriadoPor: "usuario@teste.com",
            UpdatedAt: DateTimeOffset.UtcNow);

    private static CenarioSimulacaoDto CriarCenarioDto(Guid? id = null) =>
        new(
            Id: id ?? Guid.NewGuid(),
            Nome: "Cenário Completo",
            Descricao: "Cenário de teste",
            AnoBase: 2026,
            Status: "Ativo",
            CriadoPor: "usuario@teste.com",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Simulacoes: new List<SimulacaoContratacaoDto>().AsReadOnly());

    // ── GetQuadroDivida ────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuadroDivida_SemCenario_RetornaQuadroDoAnoCorrente()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarQuadroDividaDto());
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetQuadroDividaAsync(2026, null, CancellationToken.None);

        // Assert — query enviada sem cenarioId; resultado é JSON com ano e sumário
        await mediator.Received(1).Send(
            Arg.Is<GetQuadroDividaQuery>(q => q.Ano == 2026 && q.CenarioId == null),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("ano").GetInt32().Should().Be(2026);
    }

    [Fact]
    public async Task GetQuadroDivida_ComCenario_AplicaCenarioNoQuadro()
    {
        // Arrange
        Guid cenarioId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarQuadroDividaDto(cenarioId));
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetQuadroDividaAsync(2026, cenarioId.ToString(), CancellationToken.None);

        // Assert — query enviada com o cenarioId correto
        await mediator.Received(1).Send(
            Arg.Is<GetQuadroDividaQuery>(q => q.Ano == 2026 && q.CenarioId == cenarioId),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("cenarioAplicado").GetProperty("id").GetString()
            .Should().Be(cenarioId.ToString());
    }

    [Fact]
    public async Task GetQuadroDivida_CenarioInexistente_RetornaJsonDeErro()
    {
        // Arrange — cenário não encontrado; handler lança KeyNotFoundException
        Guid cenarioId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException($"Cenário de simulação '{cenarioId}' não encontrado."));
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetQuadroDividaAsync(2026, cenarioId.ToString(), CancellationToken.None);

        // Assert — a tool captura a exceção e serializa JSON com "error"
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out JsonElement errorElem).Should().BeTrue();
        errorElem.GetString().Should().Contain(cenarioId.ToString());
    }

    [Fact]
    public async Task GetQuadroDivida_CenarioIdInvalido_RetornaJsonDeErro()
    {
        // Arrange — GUID inválido deve ser rejeitado antes de chamar o mediator
        IMediator mediator = Substitute.For<IMediator>();
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetQuadroDividaAsync(2026, "not-a-guid", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        await mediator.DidNotReceive().Send(Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>());
    }

    // ── ListCenariosSimulacao ──────────────────────────────────────────────

    [Fact]
    public async Task ListCenariosSimulacao_SemFiltros_RetornaTodos()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IReadOnlyList<CenarioSimulacaoResumoDto> lista = new List<CenarioSimulacaoResumoDto>
        {
            CriarResumoDto(status: "Ativo"),
            CriarResumoDto(status: "Rascunho"),
            CriarResumoDto(status: "Arquivado"),
        }.AsReadOnly();
        mediator.Send(Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>())
            .Returns(lista);
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.ListCenariosSimulacaoAsync(null, null, CancellationToken.None);

        // Assert — query enviada com status e anoBase nulos; resultado é array JSON com 3 itens
        await mediator.Received(1).Send(
            Arg.Is<ListCenariosSimulacaoQuery>(q => q.Status == null && q.AnoBase == null),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task ListCenariosSimulacao_FiltradoPorStatus_RetornaApenasMatching()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IReadOnlyList<CenarioSimulacaoResumoDto> lista = new List<CenarioSimulacaoResumoDto>
        {
            CriarResumoDto(status: "Ativo"),
        }.AsReadOnly();
        mediator.Send(Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>())
            .Returns(lista);
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.ListCenariosSimulacaoAsync("Ativo", null, CancellationToken.None);

        // Assert — query enviada com Status = Ativo
        await mediator.Received(1).Send(
            Arg.Is<ListCenariosSimulacaoQuery>(q => q.Status == StatusCenarioSimulacao.Ativo),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListCenariosSimulacao_FiltradoPorAnoBase_EnviaQueryComAnoBase()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacaoResumoDto>().AsReadOnly());
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        await tools.ListCenariosSimulacaoAsync(null, 2026, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<ListCenariosSimulacaoQuery>(q => q.AnoBase == 2026 && q.Status == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCenariosSimulacao_StatusInvalido_RetornaJsonDeErro()
    {
        // Arrange — enum parse falha com status desconhecido
        IMediator mediator = Substitute.For<IMediator>();
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.ListCenariosSimulacaoAsync("StatusInexistente", null, CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        await mediator.DidNotReceive().Send(
            Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>());
    }

    // ── GetCenarioSimulacao ────────────────────────────────────────────────

    [Fact]
    public async Task GetCenarioSimulacao_PorId_RetornaCenarioComSimulacoes()
    {
        // Arrange
        Guid cenarioId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetCenarioSimulacaoByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarCenarioDto(cenarioId));
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetCenarioSimulacaoAsync(cenarioId.ToString(), CancellationToken.None);

        // Assert — query enviada com o id correto; resultado contém o campo "id"
        await mediator.Received(1).Send(
            Arg.Is<GetCenarioSimulacaoByIdQuery>(q => q.Id == cenarioId),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("id").GetString().Should().Be(cenarioId.ToString());
    }

    [Fact]
    public async Task GetCenarioSimulacao_IdInexistente_RetornaJsonDeErro()
    {
        // Arrange — handler lança KeyNotFoundException para id inexistente
        Guid cenarioId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetCenarioSimulacaoByIdQuery>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException($"Cenário '{cenarioId}' não encontrado."));
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetCenarioSimulacaoAsync(cenarioId.ToString(), CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out JsonElement errorElem).Should().BeTrue();
        errorElem.GetString().Should().Contain(cenarioId.ToString());
    }

    [Fact]
    public async Task GetCenarioSimulacao_IdInvalido_RetornaJsonDeErro()
    {
        // Arrange — GUID inválido deve ser rejeitado antes de chamar o mediator
        IMediator mediator = Substitute.For<IMediator>();
        SimulacaoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetCenarioSimulacaoAsync("not-a-uuid", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        await mediator.DidNotReceive().Send(
            Arg.Any<GetCenarioSimulacaoByIdQuery>(), Arg.Any<CancellationToken>());
    }
}
