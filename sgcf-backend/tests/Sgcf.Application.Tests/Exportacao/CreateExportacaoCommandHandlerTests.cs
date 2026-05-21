using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Common;
using Sgcf.Application.Exportacao;
using Sgcf.Application.Exportacao.Commands;
using Sgcf.Domain.Exportacao;

using Xunit;

namespace Sgcf.Application.Tests.Exportacao;

/// <summary>
/// Testes unitários para <see cref="CreateExportacaoCommandHandler"/>.
/// Verifica que o job é criado com o status correto, que o repositório é chamado
/// e que o DTO retornado reflete o estado persistido.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CreateExportacaoCommandHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 21, 12, 0);
    private const string ActorSubPadrao = "user|abc123";

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static ICurrentUserService CriarCurrentUserService(string sub = ActorSubPadrao)
    {
        ICurrentUserService svc = Substitute.For<ICurrentUserService>();
        svc.ActorSub.Returns(sub);
        return svc;
    }

    private static (
        IExportacaoJobRepository repository,
        CreateExportacaoCommandHandler handler) CriarHandler(
        string actorSub = ActorSubPadrao)
    {
        IExportacaoJobRepository repository = Substitute.For<IExportacaoJobRepository>();
        repository.AddAsync(Arg.Any<ExportacaoJob>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        CreateExportacaoCommandHandler handler = new(
            repository,
            CriarClock(),
            CriarCurrentUserService(actorSub));

        return (repository, handler);
    }

    // ── Caso 1: Job criado com Status = Pendente e SolicitadoPor correto ─────────

    [Fact]
    public async Task Handle_ComandoValido_CriaJobComStatusPendenteESolicitadoPorCorreto()
    {
        // Arrange
        (IExportacaoJobRepository repository, CreateExportacaoCommandHandler handler) =
            CriarHandler(actorSub: "user|xyz789");

        CreateExportacaoCommand command = new(TipoExportacao.Contratos, ParametrosJson: null);

        // Act
        EnvelopeResponse<ExportacaoJobDto> resultado =
            await handler.Handle(command, CancellationToken.None);

        // Assert — DTO deve refletir Pendente e o sub correto
        resultado.Data.Status.Should().Be(StatusExportacao.Pendente.ToString());
        resultado.Data.SolicitadoPor.Should().Be("user|xyz789");
    }

    // ── Caso 2: AddAsync chamado com o job criado ─────────────────────────────────

    [Fact]
    public async Task Handle_ComandoValido_ChamaAddAsyncUmaVez()
    {
        // Arrange
        (IExportacaoJobRepository repository, CreateExportacaoCommandHandler handler) = CriarHandler();

        CreateExportacaoCommand command = new(TipoExportacao.FluxoCaixa, ParametrosJson: null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await repository.Received(1).AddAsync(
            Arg.Any<ExportacaoJob>(),
            Arg.Any<CancellationToken>());
    }

    // ── Caso 3: SaveChangesAsync chamado após AddAsync ────────────────────────────

    [Fact]
    public async Task Handle_ComandoValido_ChamaSaveChangesAsync()
    {
        // Arrange
        (IExportacaoJobRepository repository, CreateExportacaoCommandHandler handler) = CriarHandler();

        CreateExportacaoCommand command = new(TipoExportacao.Covenants, ParametrosJson: null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Caso 4: Retorna DTO com os campos corretos ────────────────────────────────

    [Fact]
    public async Task Handle_ComandoComParametros_RetornaDtoComCamposCorretos()
    {
        // Arrange
        const string parametros = "{\"bancoId\":\"abc\"}";

        (_, CreateExportacaoCommandHandler handler) = CriarHandler();

        CreateExportacaoCommand command = new(TipoExportacao.Alertas, ParametrosJson: parametros);

        // Act
        EnvelopeResponse<ExportacaoJobDto> resultado =
            await handler.Handle(command, CancellationToken.None);

        // Assert
        ExportacaoJobDto dto = resultado.Data;

        dto.Id.Should().NotBeEmpty();
        dto.Tipo.Should().Be(TipoExportacao.Alertas.ToString());
        dto.Status.Should().Be(StatusExportacao.Pendente.ToString());
        dto.ParametrosJson.Should().Be(parametros);
        dto.ResultadoJson.Should().BeNull();
        dto.MensagemErro.Should().BeNull();
        dto.SolicitadoPor.Should().Be(ActorSubPadrao);
        dto.CriadoEm.Should().Be(Agora);
        dto.IniciadoEm.Should().BeNull();
        dto.ConcluidoEm.Should().BeNull();
    }

    // ── Caso 5: Meta com Completude.Completo ─────────────────────────────────────

    [Fact]
    public async Task Handle_ComandoValido_MetaComCompletude_Completo()
    {
        // Arrange
        (_, CreateExportacaoCommandHandler handler) = CriarHandler();

        CreateExportacaoCommand command = new(TipoExportacao.AuditLog, ParametrosJson: null);

        // Act
        EnvelopeResponse<ExportacaoJobDto> resultado =
            await handler.Handle(command, CancellationToken.None);

        // Assert
        resultado.Meta.Completude.Should().Be(Completude.Completo);
        resultado.Meta.DataHoraCalculo.Should().Be(Agora);
    }
}
