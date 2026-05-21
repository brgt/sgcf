using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Common;
using Sgcf.Application.Covenants;
using Sgcf.Application.Covenants.Queries;
using Sgcf.Domain.Covenants;

using Xunit;

namespace Sgcf.Application.Tests.Covenants;

/// <summary>
/// Testes unitários para <see cref="GetCovenantsVioladosQueryHandler"/>.
/// Verifica a lógica de combinação e deduplicação de violados + vencendo no próximo mês.
/// </summary>
[Trait("Category", "Domain")]
public sealed class GetCovenantsVioladosQueryHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 21, 12, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static Covenant CriarCovenant(
        Guid? contratoId = null,
        string descricao = "Covenant de teste",
        TipoCovenant tipo = TipoCovenant.Financeiro,
        LocalDate? proximaVerificacao = null)
    {
        return Covenant.Criar(
            contratoId ?? Guid.NewGuid(),
            descricao,
            tipo,
            periodicidadeMeses: 3,
            proximaVerificacaoEm: proximaVerificacao,
            limiteNumerico: null,
            agora: Agora);
    }

    private static (ICovenantRepository, GetCovenantsVioladosQueryHandler) CriarHandler(
        IReadOnlyList<Covenant>? violados = null,
        IReadOnlyList<Covenant>? vencendo = null)
    {
        ICovenantRepository repository = Substitute.For<ICovenantRepository>();

        repository.ListVioladosAsync(Arg.Any<CancellationToken>())
            .Returns(violados ?? Array.Empty<Covenant>());

        repository.ListVencendoAsync(Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(vencendo ?? Array.Empty<Covenant>());

        GetCovenantsVioladosQueryHandler handler = new(repository, CriarClock());
        return (repository, handler);
    }

    // ── Caso 1: Covenant presente em ambas as listas aparece apenas uma vez ──────

    [Fact]
    public async Task Handle_CovenantEmAmbasAsListas_AparceUmaVezNoResultado()
    {
        // Arrange — mesmo covenant está tanto em violados quanto em vencendo
        Covenant covenant = CriarCovenant(descricao: "Índice de cobertura");

        (_, GetCovenantsVioladosQueryHandler handler) = CriarHandler(
            violados: [covenant],
            vencendo: [covenant]);

        // Act
        EnvelopeResponse<IReadOnlyList<CovenantDto>> resultado =
            await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert — deduplicação por Id: deve aparecer exatamente uma vez
        resultado.Data.Should().HaveCount(1);
        resultado.Data.Single().Id.Should().Be(covenant.Id);
    }

    // ── Caso 2: Covenant só na lista de vencendo aparece no resultado ────────────

    [Fact]
    public async Task Handle_CovenantApenasNaListaVencendo_AparceNoResultado()
    {
        // Arrange — nenhum violado; apenas um covenant vencendo no próximo mês
        Covenant covenantVencendo = CriarCovenant(descricao: "Covenant vencendo em breve");

        (_, GetCovenantsVioladosQueryHandler handler) = CriarHandler(
            violados: [],
            vencendo: [covenantVencendo]);

        // Act
        EnvelopeResponse<IReadOnlyList<CovenantDto>> resultado =
            await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Should().HaveCount(1);
        resultado.Data.Single().Id.Should().Be(covenantVencendo.Id);
    }

    // ── Caso 3: Listas vazias retornam envelope vazio com Completude.Completo ───

    [Fact]
    public async Task Handle_ListasVazias_RetornaEnvelopeVazioComCompletude()
    {
        // Arrange
        (_, GetCovenantsVioladosQueryHandler handler) = CriarHandler(
            violados: [],
            vencendo: []);

        // Act
        EnvelopeResponse<IReadOnlyList<CovenantDto>> resultado =
            await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Should().BeEmpty();
        resultado.Meta.Completude.Should().Be(Completude.Completo);
    }

    // ── Caso 4: Covenant apenas violado (fora da janela de vencimento) aparece ──

    [Fact]
    public async Task Handle_CovenantApenasViolado_AparceNoResultado()
    {
        // Arrange — um covenant violado; lista de vencendo vazia
        Covenant covenantViolado = CriarCovenant(descricao: "Covenant violado");

        (_, GetCovenantsVioladosQueryHandler handler) = CriarHandler(
            violados: [covenantViolado],
            vencendo: []);

        // Act
        EnvelopeResponse<IReadOnlyList<CovenantDto>> resultado =
            await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Should().HaveCount(1);
        resultado.Data.Single().Id.Should().Be(covenantViolado.Id);
    }

    // ── Caso 5: Total correto quando ambas as listas têm itens únicos ────────────

    [Fact]
    public async Task Handle_ItensSemSobreposicao_TotalCorrespondeSomaDeAmbas()
    {
        // Arrange — dois covenants distintos: um violado, outro vencendo
        Covenant covenantViolado = CriarCovenant(descricao: "Violado A");
        Covenant covenantVencendo = CriarCovenant(descricao: "Vencendo B");

        (_, GetCovenantsVioladosQueryHandler handler) = CriarHandler(
            violados: [covenantViolado],
            vencendo: [covenantVencendo]);

        // Act
        EnvelopeResponse<IReadOnlyList<CovenantDto>> resultado =
            await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert — dois covenants distintos, portanto dois DTOs
        resultado.Data.Should().HaveCount(2);
        resultado.Data.Select(d => d.Id).Should().BeEquivalentTo(
            new[] { covenantViolado.Id, covenantVencendo.Id });
        resultado.Meta.Completude.Should().Be(Completude.Completo);
    }

    // ── Verificação da data de corte: ListVencendoAsync chamada com hoje + 1 mês ─

    [Fact]
    public async Task Handle_ChamaListVencendoAsyncComHojeMaisUmMes()
    {
        // Arrange
        // Agora = 2026-05-21T12:00Z → hoje em Brasília = 2026-05-21 → corte = 2026-06-21
        LocalDate corteEsperado = new LocalDate(2026, 6, 21);

        (ICovenantRepository repository, GetCovenantsVioladosQueryHandler handler) = CriarHandler();

        // Act
        await handler.Handle(new GetCovenantsVioladosQuery(), CancellationToken.None);

        // Assert
        await repository.Received(1).ListVencendoAsync(
            corteEsperado,
            Arg.Any<CancellationToken>());
    }
}
