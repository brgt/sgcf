using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class EconomiaNegociacaoTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();

    private static EconomiaNegociacao CriarEconomia(
        decimal economiaBrl = 10_000m,
        decimal economiaAjustadaBrl = 9_500m) =>
        EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: """{"cetAa": 7.5}""",
            snapshotContratoJson: """{"cetAa": 7.2}""",
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(economiaBrl, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(economiaAjustadaBrl, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

    // ─── Factory ─────────────────────────────────────────────────────────────

    [Fact]
    public void Criar_economia_valida_deve_preencher_todos_campos()
    {
        var economia = CriarEconomia(economiaBrl: 10_000m, economiaAjustadaBrl: 9_500m);

        economia.CetPropostaAaPercentual.Should().Be(7.5m);
        economia.CetContratoAaPercentual.Should().Be(7.2m);
        economia.EconomiaBrl.Valor.Should().Be(10_000m);
        economia.EconomiaAjustadaCdiBrl.Valor.Should().Be(9_500m);
        economia.DataReferenciaCdi.Should().Be(new LocalDate(2026, 5, 15));
        economia.CreatedAt.Should().NotBe(default(Instant));
    }

    [Fact]
    public void Criar_economia_com_CotacaoId_vazio_deve_lancar_excecao()
    {
        var act = () => EconomiaNegociacao.Criar(
            cotacaoId: Guid.Empty,
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: "{}",
            snapshotContratoJson: "{}",
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(1000m, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(900m, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*CotacaoId*");
    }

    [Fact]
    public void Criar_economia_com_ContratoId_vazio_deve_lancar_excecao()
    {
        var act = () => EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.Empty,
            snapshotPropostaJson: "{}",
            snapshotContratoJson: "{}",
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(1000m, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(900m, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ContratoId*");
    }

    [Fact]
    public void Criar_economia_com_snapshot_vazio_deve_lancar_excecao()
    {
        var act = () => EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: "",
            snapshotContratoJson: "{}",
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(1000m, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(900m, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Snapshot*");
    }

    [Fact]
    public void Criar_economia_com_moeda_nao_BRL_deve_lancar_excecao()
    {
        var act = () => EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: "{}",
            snapshotContratoJson: "{}",
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(1000m, Moeda.Usd), // moeda errada
            economiaAjustadaCdiBrl: new Money(900m, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ─── Imutabilidade ───────────────────────────────────────────────────────

    [Fact]
    public void EconomiaNegociacao_nao_expoe_metodos_mutadores_publicos()
    {
        var tipo = typeof(EconomiaNegociacao);
        var metodosPublicosNaoHerdados = tipo
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        metodosPublicosNaoHerdados.Should().NotContain(
            m => m.StartsWith("Set") || m.StartsWith("Atualizar") || m.StartsWith("Alterar"),
            "EconomiaNegociacao é imutável após criação (SPEC §12.3)");
    }

    [Fact]
    public void EconomiaNegociacao_pode_ter_economia_negativa_representando_perda()
    {
        var economia = CriarEconomia(economiaBrl: -5_000m, economiaAjustadaBrl: -4_800m);

        economia.EconomiaBrl.Valor.Should().BeNegative();
        economia.EconomiaAjustadaCdiBrl.Valor.Should().BeNegative();
    }

    [Fact]
    public void EconomiaNegociacao_preserva_snapshots_como_strings_imutaveis()
    {
        const string snapshotProposta = """{"cet": 7.5, "prazo": 180}""";
        const string snapshotContrato = """{"cet": 7.2, "prazo": 180}""";

        var economia = EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: snapshotProposta,
            snapshotContratoJson: snapshotContrato,
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            economiaBrl: new Money(1000m, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(950m, Moeda.Brl),
            dataReferenciaCdi: new LocalDate(2026, 5, 15),
            clock: Clock);

        economia.SnapshotPropostaJson.Should().Be(snapshotProposta);
        economia.SnapshotContratoJson.Should().Be(snapshotContrato);
    }
}
