using System.Text.Json;

using FluentAssertions;

using MediatR;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Queries;
using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

[Trait("Category", "Mcp")]
public sealed class ContratoToolsTests
{
    private static ContratoTools CriarTools(IMediator mediator) => new(mediator);

    private static ContratoDto CriarContratoDto(Guid? id = null) =>
        new(
            Id: id ?? Guid.NewGuid(),
            NumeroExterno: "FIN-2026-001",
            CodigoInterno: null,
            BancoId: Guid.NewGuid(),
            Modalidade: "Finimp",
            Moeda: "Usd",
            ValorPrincipal: 500_000m,
            DataContratacao: new DateOnly(2026, 1, 15),
            DataVencimento: new DateOnly(2028, 1, 15),
            TaxaAa: 6m,
            BaseCalculo: "Dias360",
            Status: "Ativo",
            ContratoPaiId: null,
            Observacoes: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Parcelas: new List<ParcelaDto>().AsReadOnly(),
            Garantias: new List<GarantiaResumoDto>().AsReadOnly(),
            FinimpDetail: null,
            Lei4131Detail: null,
            RefinimpDetail: null,
            NceDetail: null,
            BalcaoCaixaDetail: null,
            FgiDetail: null);

    // ── ListContratos ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListContratos_EnviaQueryCorreta_RetornaJsonPaginado()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        IReadOnlyList<ContratoDto> itens = new List<ContratoDto>
        {
            CriarContratoDto(),
            CriarContratoDto()
        }.AsReadOnly();
        PagedResult<ContratoDto> paginado = new(itens, Total: 2, Page: 1, PageSize: 25);
        mediator.Send(Arg.Any<ListContratosQuery>(), Arg.Any<CancellationToken>())
            .Returns(paginado);
        ContratoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.ListContratosAsync(search: null, page: 1, pageSize: 25, CancellationToken.None);

        // Assert — mediator foi chamado e resultado é objeto PagedResult com Items, Total, Page, PageSize
        await mediator.Received(1).Send(Arg.Any<ListContratosQuery>(), Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(2);
    }

    // ── GetContrato ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetContrato_IdValido_EnviaQueryCorreta()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetContratoQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarContratoDto(contratoId));
        ContratoTools tools = CriarTools(mediator);

        // Act
        await tools.GetContratoAsync(contratoId.ToString(), CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<GetContratoQuery>(q => q.Id == contratoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetContrato_IdInvalido_RetornaJsonDeErro()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        ContratoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetContratoAsync("not-a-uuid", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        await mediator.DidNotReceive().Send(Arg.Any<GetContratoQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetContrato_NaoEncontrado_RetornaJsonDeErro()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetContratoQuery>(), Arg.Any<CancellationToken>())
            .Throws(new KeyNotFoundException($"Contrato com Id '{contratoId}' não encontrado."));
        ContratoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetContratoAsync(contratoId.ToString(), CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    // ── GetTabelaCompleta ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTabelaCompleta_IdInvalido_RetornaJsonDeErro()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        ContratoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetTabelaCompletaAsync("not-a-uuid", null, CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        await mediator.DidNotReceive().Send(Arg.Any<GetTabelaCompletaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTabelaCompleta_DataRefNull_EnviaQueryComDataRefNull()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetTabelaCompletaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarTabelaCompletaDto(contratoId));
        ContratoTools tools = CriarTools(mediator);

        // Act
        await tools.GetTabelaCompletaAsync(contratoId.ToString(), null, CancellationToken.None);

        // Assert — DataReferencia deve ser null quando não fornecida
        await mediator.Received(1).Send(
            Arg.Is<GetTabelaCompletaQuery>(q => q.Id == contratoId && q.DataReferencia == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTabelaCompleta_DataRefValida_EnviaQueryComDataCorreta()
    {
        // Arrange
        Guid contratoId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetTabelaCompletaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarTabelaCompletaDto(contratoId));
        ContratoTools tools = CriarTools(mediator);

        // Act
        await tools.GetTabelaCompletaAsync(contratoId.ToString(), "2026-03-15", CancellationToken.None);

        // Assert — DataReferencia deve refletir a data informada
        await mediator.Received(1).Send(
            Arg.Is<GetTabelaCompletaQuery>(q =>
                q.Id == contratoId &&
                q.DataReferencia == new NodaTime.LocalDate(2026, 3, 15)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTabelaCompleta_DataRefInvalida_EnviaQueryComDataRefNull()
    {
        // Arrange — data inválida é silenciosamente ignorada (dataRef = null)
        Guid contratoId = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetTabelaCompletaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarTabelaCompletaDto(contratoId));
        ContratoTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetTabelaCompletaAsync(contratoId.ToString(), "not-a-date", CancellationToken.None);

        // Assert — data inválida não causa erro JSON; query é enviada com dataRef null
        await mediator.Received(1).Send(
            Arg.Is<GetTabelaCompletaQuery>(q => q.Id == contratoId && q.DataReferencia == null),
            Arg.Any<CancellationToken>());
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeFalse();
    }

    // ── Helper privado ─────────────────────────────────────────────────────

    private static TabelaCompletaDto CriarTabelaCompletaDto(Guid contratoId) =>
        new(
            Identificacao: new IdentificacaoDto(
                Id: contratoId,
                CodigoInterno: "FIN-2026-001",
                Banco: Guid.NewGuid().ToString(),
                Modalidade: "Finimp",
                NumeroContratoExterno: "FIN-2026-001",
                DataContratacao: new DateOnly(2026, 1, 15),
                DataVencimento: new DateOnly(2028, 1, 15),
                Status: "Ativo"),
            ValoresPrincipais: new ValoresPrincipaisDto(500_000m, "Usd"),
            Encargos: new EncargosDto(6m, "Dias360", null, null),
            ResumoFinanceiro: new ResumoFinanceiroDto(
                TotalPrincipalPago: 0m,
                TotalJurosPagos: 0m,
                TotalComissoesPagas: 0m,
                Moeda: "Usd",
                SaldoPrincipalAberto: 500_000m,
                JurosProvisionados: 0m,
                ComissoesAPagar: 0m,
                SaldoTotalDevedor: 500_000m,
                SaldoPrincipalAbertoBrl: null,
                JurosProvisionadosBrl: null,
                ComissoesAPagarBrl: null,
                SaldoTotalDevedorBrl: null,
                TotalEventos: 0,
                EventosPagos: 0,
                EventosEmAberto: 0,
                EventosEmAtraso: 0,
                PctAdimplencia: 0m,
                ProximaParcela: null,
                ValorProximaParcela: null,
                PctPrincipalAmortizado: 0m,
                PctPrazoDecorrido: 0m),
            Cronograma: new List<EventoCronogramaDto>().AsReadOnly(),
            Garantias: new List<GarantiaResumoDto>().AsReadOnly(),
            Hedge: new HedgePlaceholderDto("Hedge não configurado."),
            HistoricoPagamentos: new List<PagamentoEfetivoDto>().AsReadOnly(),
            CotacaoAplicada: null);
}
