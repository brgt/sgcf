using System.Security.Claims;

using FluentAssertions;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Sgcf.Application.Authorization;
using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

/// <summary>
/// Testes de autorização para SimulacaoTools.
/// Verifica que cada tool exige a policy Leitura antes de invocar o mediator.
///
/// Estes testes foram escritos no estado RED — falham enquanto SimulacaoTools
/// não verifica IAuthorizationService (security fix #1).
/// </summary>
[Trait("Category", "Mcp")]
[Trait("Category", "Security")]
public sealed class SimulacaoToolsAuthTests
{
    // ── Fábrica de ferramentas ─────────────────────────────────────────────────

    /// <summary>
    /// Cria SimulacaoTools com IAuthorizationService configurado para aprovar ou rejeitar
    /// a policy <paramref name="policy"/> dependendo de <paramref name="autorizado"/>.
    /// </summary>
    private static (SimulacaoTools Tools, IMediator Mediator) CriarTools(
        bool autorizado,
        string policy = Policies.Leitura,
        ClaimsPrincipal? usuario = null)
    {
        IMediator mediator = Substitute.For<IMediator>();
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        IAuthorizationService authorizationService = Substitute.For<IAuthorizationService>();

        ClaimsPrincipal principal = usuario ?? CriarUsuario("usuario-teste");

        HttpContext httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);
        httpContextAccessor.HttpContext.Returns(httpContext);

        AuthorizationResult resultado = autorizado
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed();

        authorizationService
            .AuthorizeAsync(principal, null, policy)
            .Returns(resultado);

        SimulacaoTools tools = new(mediator, httpContextAccessor, authorizationService);
        return (tools, mediator);
    }

    /// <summary>
    /// Cria SimulacaoTools sem HttpContext (simula requisição sem identidade).
    /// </summary>
    private static SimulacaoTools CriarToolsSemContexto()
    {
        IMediator mediator = Substitute.For<IMediator>();
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        IAuthorizationService authorizationService = Substitute.For<IAuthorizationService>();

        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        return new SimulacaoTools(mediator, httpContextAccessor, authorizationService);
    }

    private static ClaimsPrincipal CriarUsuario(string sub, params string[] roles)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, sub)];
        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    // ── GetQuadroDivida ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuadroDivida_SemHttpContext_LancaUnauthorizedAccessException()
    {
        // Arrange
        SimulacaoTools tools = CriarToolsSemContexto();

        // Act
        Func<Task> act = () => tools.GetQuadroDividaAsync(2026, null, CancellationToken.None);

        // Assert — sem contexto HTTP não há identidade; a tool deve rejeitar imediatamente
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*identidade*");
    }

    [Fact]
    public async Task GetQuadroDivida_UsuarioSemPolicyLeitura_LancaUnauthorizedAccessException()
    {
        // Arrange — autorizado = false simula auditor sem Leitura
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: false);

        // Act
        Func<Task> act = () => tools.GetQuadroDividaAsync(2026, null, CancellationToken.None);

        // Assert — autorização falha; mediator não deve ser invocado
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*{Policies.Leitura}*");
        await mediator.DidNotReceive().Send(
            Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetQuadroDivida_UsuarioComPolicyLeitura_InvocaMediatorNormalmente()
    {
        // Arrange — autorizado = true; mediator retorna DTO mínimo
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: true);
        mediator.Send(Arg.Any<GetQuadroDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(SimulacaoToolsTestHelpers.CriarQuadroDividaDto());

        // Act — não deve lançar exceção
        string resultado = await tools.GetQuadroDividaAsync(2026, null, CancellationToken.None);

        // Assert — mediator deve ter sido chamado exatamente uma vez
        await mediator.Received(1).Send(
            Arg.Is<GetQuadroDividaQuery>(q => q.Ano == 2026 && q.CenarioId == null),
            Arg.Any<CancellationToken>());
        resultado.Should().NotBeNullOrEmpty();
    }

    // ── ListCenariosSimulacao ──────────────────────────────────────────────────

    [Fact]
    public async Task ListCenariosSimulacao_SemHttpContext_LancaUnauthorizedAccessException()
    {
        // Arrange
        SimulacaoTools tools = CriarToolsSemContexto();

        // Act
        Func<Task> act = () => tools.ListCenariosSimulacaoAsync(null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*identidade*");
    }

    [Fact]
    public async Task ListCenariosSimulacao_UsuarioSemPolicyLeitura_LancaUnauthorizedAccessException()
    {
        // Arrange
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: false);

        // Act
        Func<Task> act = () => tools.ListCenariosSimulacaoAsync(null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*{Policies.Leitura}*");
        await mediator.DidNotReceive().Send(
            Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCenariosSimulacao_UsuarioComPolicyLeitura_InvocaMediatorNormalmente()
    {
        // Arrange
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: true);
        mediator.Send(Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Sgcf.Application.Simulacao.Dtos.CenarioSimulacaoResumoDto>().AsReadOnly());

        // Act
        string resultado = await tools.ListCenariosSimulacaoAsync(null, null, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Any<ListCenariosSimulacaoQuery>(), Arg.Any<CancellationToken>());
        resultado.Should().NotBeNullOrEmpty();
    }

    // ── GetCenarioSimulacao ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCenarioSimulacao_SemHttpContext_LancaUnauthorizedAccessException()
    {
        // Arrange
        SimulacaoTools tools = CriarToolsSemContexto();

        // Act
        Func<Task> act = () => tools.GetCenarioSimulacaoAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*identidade*");
    }

    [Fact]
    public async Task GetCenarioSimulacao_UsuarioSemPolicyLeitura_LancaUnauthorizedAccessException()
    {
        // Arrange
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: false);

        // Act
        Func<Task> act = () => tools.GetCenarioSimulacaoAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*{Policies.Leitura}*");
        await mediator.DidNotReceive().Send(
            Arg.Any<GetCenarioSimulacaoByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCenarioSimulacao_UsuarioComPolicyLeitura_InvocaMediatorNormalmente()
    {
        // Arrange
        Guid cenarioId = Guid.NewGuid();
        (SimulacaoTools tools, IMediator mediator) = CriarTools(autorizado: true);
        mediator.Send(Arg.Any<GetCenarioSimulacaoByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(SimulacaoToolsTestHelpers.CriarCenarioDto(cenarioId));

        // Act
        string resultado = await tools.GetCenarioSimulacaoAsync(cenarioId.ToString(), CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<GetCenarioSimulacaoByIdQuery>(q => q.Id == cenarioId),
            Arg.Any<CancellationToken>());
        resultado.Should().NotBeNullOrEmpty();
    }
}
