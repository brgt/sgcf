using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Common;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Painel.EconomiaTributaria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes unitários para <see cref="GetEconomiaTributariaQueryHandler"/>.
/// Verifica o cálculo do benefício tributário estimado (alíquota combinada 34%)
/// e a correta agregação de economias por período.
/// </summary>
[Trait("Category", "Domain")]
public sealed class GetEconomiaTributariaQueryHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 21, 12, 0);
    private static readonly LocalDate DataReferenciaCdi = new(2026, 5, 1);

    private static readonly GetEconomiaTributariaQuery QueryPadrao = new(
        DeAno: 2026,
        DeMes: 1,
        AteAno: 2026,
        AteMes: 5,
        BancoId: null);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static EconomiaNegociacao CriarEconomia(
        decimal economiaBrl,
        decimal economiaAjustadaCdiBrl,
        Guid? bancoId = null,
        IClock? clock = null)
    {
        IClock clockEfetivo = clock ?? CriarClock();

        string snapshotProposta = bancoId.HasValue
            ? $"{{\"BancoId\":\"{bancoId.Value}\"}}"
            : "{}";

        return EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: snapshotProposta,
            snapshotContratoJson: "{\"numero\":\"FIN-2026-001\"}",
            cetPropostaAaPercentual: 12.5m,
            cetContratoAaPercentual: 10.0m,
            economiaBrl: new Money(economiaBrl, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(economiaAjustadaCdiBrl, Moeda.Brl),
            dataReferenciaCdi: DataReferenciaCdi,
            clock: clockEfetivo);
    }

    private static (IEconomiaRepository, GetEconomiaTributariaQueryHandler) CriarHandler(
        IReadOnlyList<EconomiaNegociacao>? economias = null)
    {
        IEconomiaRepository repository = Substitute.For<IEconomiaRepository>();

        repository.ListByPeriodoAsync(
                Arg.Any<YearMonth>(),
                Arg.Any<YearMonth>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(economias ?? Array.Empty<EconomiaNegociacao>());

        GetEconomiaTributariaQueryHandler handler = new(repository, CriarClock());
        return (repository, handler);
    }

    // ── Caso 1: Cálculo correto do benefício tributário = ajustadaCdi × 0,34 ────

    [Fact]
    public async Task Handle_UmaEconomia_BeneficioTributarioCalculadoCorretamente()
    {
        // Arrange — economiaAjustadaCdiBrl = R$ 100.000,00
        // Benefício esperado = 100.000 × 0,34 = R$ 34.000,00
        EconomiaNegociacao economia = CriarEconomia(
            economiaBrl: 90_000m,
            economiaAjustadaCdiBrl: 100_000m);

        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([economia]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert
        resultado.Data.BeneficioTributarioEstimadoBrl.Should().Be(34_000.00m);
    }

    // ── Caso 2: Lista vazia retorna zeros ─────────────────────────────────────────

    [Fact]
    public async Task Handle_ListaVazia_RetornaZerosEmTodosOsCampos()
    {
        // Arrange
        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert
        resultado.Data.TotalEconomiaBrl.Should().Be(0m);
        resultado.Data.TotalEconomiaAjustadaCdiBrl.Should().Be(0m);
        resultado.Data.BeneficioTributarioEstimadoBrl.Should().Be(0m);
        resultado.Data.TotalOperacoes.Should().Be(0);
        resultado.Data.PorBanco.Should().BeEmpty();
    }

    // ── Caso 3: Múltiplas economias somam corretamente ───────────────────────────

    [Fact]
    public async Task Handle_MultiplaEconomias_SomasTotaisCorretas()
    {
        // Arrange
        // economiaBrl: 50k + 30k = 80k
        // economiaAjustadaCdiBrl: 60k + 40k = 100k
        // benefício = 100k × 0,34 = 34k
        Guid bancoId = Guid.NewGuid();
        EconomiaNegociacao e1 = CriarEconomia(economiaBrl: 50_000m, economiaAjustadaCdiBrl: 60_000m, bancoId: bancoId);
        EconomiaNegociacao e2 = CriarEconomia(economiaBrl: 30_000m, economiaAjustadaCdiBrl: 40_000m, bancoId: bancoId);

        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([e1, e2]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert
        resultado.Data.TotalEconomiaBrl.Should().Be(80_000.00m);
        resultado.Data.TotalEconomiaAjustadaCdiBrl.Should().Be(100_000.00m);
        resultado.Data.BeneficioTributarioEstimadoBrl.Should().Be(34_000.00m);
        resultado.Data.TotalOperacoes.Should().Be(2);
    }

    // ── Caso 4: BancoId null no resultado quando query não filtra por banco ───────

    [Fact]
    public async Task Handle_SemFiltroDebanco_SnapshotSemBancoIdAgregaSobNull()
    {
        // Arrange — snapshot da proposta sem BancoId (agrupado sob null)
        EconomiaNegociacao economia = CriarEconomia(
            economiaBrl: 10_000m,
            economiaAjustadaCdiBrl: 12_000m,
            bancoId: null);

        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([economia]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert — o grupo deve ter BancoId null
        resultado.Data.PorBanco.Should().HaveCount(1);
        resultado.Data.PorBanco.Single().BancoId.Should().BeNull();
    }

    // ── Caso 5: Completude sempre Completo ────────────────────────────────────────

    [Fact]
    public async Task Handle_SempRetornaCompletude_Completo()
    {
        // Arrange
        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert
        resultado.Meta.Completude.Should().Be(Completude.Completo);
    }

    // ── Caso 6: Arredondamento AwayFromZero aplicado corretamente ─────────────────

    [Fact]
    public async Task Handle_ValorQueProduzFracao5_ArredondaParaCima()
    {
        // Arrange — 1.005 × 0,34 = 0,3417 → AwayFromZero → 0,34
        // Para testar arredondamento: economiaAjustadaCdiBrl = 1,005 (3 casas)
        // Math.Round(1.005m * 0.34m, 2, AwayFromZero) = Math.Round(0.3417m, 2, AwayFromZero) = 0.34
        EconomiaNegociacao economia = CriarEconomia(
            economiaBrl: 1.00m,
            economiaAjustadaCdiBrl: 1.005m);

        (_, GetEconomiaTributariaQueryHandler handler) = CriarHandler([economia]);

        // Act
        EnvelopeResponse<EconomiaTributariaDto> resultado =
            await handler.Handle(QueryPadrao, CancellationToken.None);

        // Assert — benefício = 1,005 × 0,34 = 0,3417 → arredondado = 0,34
        resultado.Data.BeneficioTributarioEstimadoBrl.Should().Be(
            Math.Round(1.005m * 0.34m, 2, MidpointRounding.AwayFromZero));
    }
}
