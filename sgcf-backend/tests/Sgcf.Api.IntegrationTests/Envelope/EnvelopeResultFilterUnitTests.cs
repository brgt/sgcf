using System.Globalization;
using System.Reflection;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

using NodaTime;

using NSubstitute;

using Sgcf.Api.Filters;
using Sgcf.Application.Common;

using Xunit;

namespace Sgcf.Api.IntegrationTests.Envelope;

/// <summary>
/// Testes unitários do <see cref="EnvelopeResultFilter"/>.
///
/// Cenários:
///   1. Endpoint COM [ProducesEnvelope] + ObjectResult → resultado é envelopado.
///   2. Endpoint SEM [ProducesEnvelope] → resultado passa inalterado.
///   3. Endpoint COM [ProducesEnvelope] + resultado já é EnvelopeResponse → não re-envolve.
///   4. Endpoint COM [ProducesEnvelope] + NoContentResult → passa inalterado.
///   5. DataHoraCalculo usa o instante fornecido pelo IClock injetado.
/// </summary>
public sealed class EnvelopeResultFilterUnitTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 10, 0);

    private static IClock CriaClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    /// <summary>
    /// Constrói um <see cref="ResultExecutingContext"/> com metadados de endpoint
    /// controlados para simular a presença ou ausência de [ProducesEnvelope].
    /// </summary>
    private static ResultExecutingContext CriaContexto(
        IActionResult resultado,
        bool comAtributoEnvelope)
    {
        List<object> endpointMetadata = [];
        if (comAtributoEnvelope)
        {
            endpointMetadata.Add(new ProducesEnvelopeAttribute());
        }

        ActionDescriptor actionDescriptor = new()
        {
            EndpointMetadata = endpointMetadata
        };

        ActionContext actionContext = new(
            new DefaultHttpContext(),
            new RouteData(),
            actionDescriptor);

        return new ResultExecutingContext(
            actionContext,
            filters: [],
            result: resultado,
            controller: new object());
    }

    /// <summary>
    /// Cria um <see cref="ResultExecutionDelegate"/> que devolve um contexto executado vazio.
    /// <c>ResultExecutionDelegate</c> é <c>Func&lt;Task&lt;ResultExecutedContext&gt;&gt;</c>,
    /// portanto não pode ser substituído por <c>() =&gt; Task.CompletedTask</c>.
    /// </summary>
    private static ResultExecutionDelegate CriaNext(ResultExecutingContext ctx) =>
        () => Task.FromResult(new ResultExecutedContext(ctx, [], ctx.Result, new object()));

    // ── Teste 1: envelopa quando [ProducesEnvelope] está presente ────────────

    [Fact]
    public async Task OnResultExecutionAsync_ComAtributo_EnvelopaObjectResult()
    {
        // Arrange
        EnvelopeResultFilter filtro = new(CriaClock());
        object payload = new { nome = "teste", valor = 99 };
        ResultExecutingContext ctx = CriaContexto(new OkObjectResult(payload), comAtributoEnvelope: true);

        bool nextFoiChamado = false;
        ResultExecutionDelegate next = () =>
        {
            nextFoiChamado = true;
            return Task.FromResult(new ResultExecutedContext(ctx, [], ctx.Result, new object()));
        };

        // Act
        await filtro.OnResultExecutionAsync(ctx, next);

        // Assert
        nextFoiChamado.Should().BeTrue(because: "next() deve sempre ser chamado pelo filtro");

        ctx.Result.Should().BeOfType<ObjectResult>();
        ObjectResult result = (ObjectResult)ctx.Result;

        result.Value.Should().NotBeNull();
        result.Value!.GetType().IsGenericType.Should().BeTrue();
        result.Value!.GetType().GetGenericTypeDefinition()
            .Should().Be(typeof(EnvelopeResponse<>),
                because: "o resultado deve ser envelopado em EnvelopeResponse<T>");
    }

    // ── Teste 2: não envelopa sem [ProducesEnvelope] ──────────────────────────

    [Fact]
    public async Task OnResultExecutionAsync_SemAtributo_PassaResultadoSemModificar()
    {
        // Arrange
        EnvelopeResultFilter filtro = new(CriaClock());
        object payload = new { nome = "direto" };
        OkObjectResult resultadoOriginal = new(payload);
        ResultExecutingContext ctx = CriaContexto(resultadoOriginal, comAtributoEnvelope: false);

        // Act
        await filtro.OnResultExecutionAsync(ctx, CriaNext(ctx));

        // Assert — resultado não foi substituído
        ctx.Result.Should().BeSameAs(resultadoOriginal,
            because: "sem [ProducesEnvelope] o filtro não deve tocar no resultado");
    }

    // ── Teste 3: não re-envelopa EnvelopeResponse<T> existente ───────────────

    [Fact]
    public async Task OnResultExecutionAsync_ComAtributo_NaoReEnvelopaEnvelopeResponseExistente()
    {
        // Arrange
        EnvelopeResultFilter filtro = new(CriaClock());

        EnvelopeMeta metaExistente = new(
            DataHoraCalculo:   InstanteFixo,
            FontesConsultadas: [new FonteConsultada("banco_de_dados", "ok", 5)],
            Completude:        Completude.Parcial);

        EnvelopeResponse<string> envelopeJaFeito = new("dado enriquecido", metaExistente);
        OkObjectResult resultadoOriginal = new(envelopeJaFeito);
        ResultExecutingContext ctx = CriaContexto(resultadoOriginal, comAtributoEnvelope: true);

        // Act
        await filtro.OnResultExecutionAsync(ctx, CriaNext(ctx));

        // Assert — resultado envelopado pelo handler não foi re-envolvido
        ctx.Result.Should().BeSameAs(resultadoOriginal,
            because: "EnvelopeResponse<T> produzido pelo handler não deve ser re-envolvido");
    }

    // ── Teste 4: NoContentResult passa sem modificação ────────────────────────

    [Fact]
    public async Task OnResultExecutionAsync_ComAtributo_NoContentPassaSemModificar()
    {
        // Arrange
        EnvelopeResultFilter filtro = new(CriaClock());
        NoContentResult semConteudo = new();
        ResultExecutingContext ctx = CriaContexto(semConteudo, comAtributoEnvelope: true);

        // Act
        await filtro.OnResultExecutionAsync(ctx, CriaNext(ctx));

        // Assert — NoContentResult não tem valor para envolver
        ctx.Result.Should().BeSameAs(semConteudo,
            because: "NoContentResult não tem corpo — filtro deve passar sem modificar");
    }

    // ── Teste 5: DataHoraCalculo usa IClock injetado ──────────────────────────

    [Fact]
    public async Task OnResultExecutionAsync_ComAtributo_DataHoraCalculoEhInstanteDoClock()
    {
        // Arrange
        EnvelopeResultFilter filtro = new(CriaClock());
        ResultExecutingContext ctx = CriaContexto(new OkObjectResult("payload"), comAtributoEnvelope: true);

        // Act
        await filtro.OnResultExecutionAsync(ctx, CriaNext(ctx));

        // Assert
        ObjectResult result = (ObjectResult)ctx.Result;
        object envelope = result.Value!;

        // Extrai a propriedade Meta via reflection (tipo genérico em runtime).
        // Necessário porque o tipo exato de EnvelopeResponse<T> só é conhecido em tempo de execução.
        PropertyInfo metaProp = envelope.GetType().GetProperty("Meta")!;
        EnvelopeMeta meta = (EnvelopeMeta)metaProp.GetValue(envelope)!;

        meta.DataHoraCalculo.Should().Be(InstanteFixo,
            because: "filtro deve usar IClock.GetCurrentInstant(), não DateTime.UtcNow");

        meta.Completude.Should().Be(Completude.Completo,
            because: "completude padrão mínima deve ser Completo");

        meta.FontesConsultadas.Should().BeEmpty(
            because: "filtro mínimo não preenche fontes — responsabilidade do handler");
    }
}
