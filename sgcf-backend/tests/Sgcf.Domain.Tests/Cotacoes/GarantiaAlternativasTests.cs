using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes das invariantes de Grupos de Alternativas "OU" (GA-01..GA-07).
/// SPEC_GARANTIAS_ALTERNATIVAS §4.2. Foco em domínio (item + agregado revisão).
/// </summary>
public sealed class GarantiaAlternativasTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();

    private static IClock CriarClock() => PropostaFactory.CriarClockFixo(2026, 5, 25);

    private static GarantiaExigidaItemSpec SpecGrupo(
        TipoGarantia tipo, Guid grupoId, decimal percentual = 100m,
        string? rotulo = "Colateral FINIMP", bool obrigatoria = false) =>
        new(tipo, percentual, null, obrigatoria, null, grupoId, rotulo);

    // ─── GA-01: item sem grupo preserva semântica legada ──────────────────────

    [Fact]
    public void ItemSemGrupo_PreservaObrigatoriaEGrupoNulo()
    {
        var revisao = GarantiaExigidaRevisao.Criar(
            LimiteId,
            [new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 20m, null, false, null)],
            CriarClock());

        var item = revisao.Itens.Single();
        item.GrupoAlternativaId.Should().BeNull();
        item.GrupoRotulo.Should().BeNull();
        item.Obrigatoria.Should().BeFalse("item sem grupo mantém o flag informado (GA-01)");
    }

    // ─── GA-04: item agrupado é sempre obrigatório (normalização) ─────────────

    [Fact]
    public void ItemAgrupado_NormalizaObrigatoriaParaTrue()
    {
        var grupo = Guid.NewGuid();
        var revisao = GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupo, obrigatoria: false),
                SpecGrupo(TipoGarantia.BoletoBancario, grupo, percentual: 90m, obrigatoria: false),
            ],
            CriarClock());

        revisao.Itens.Should().OnlyContain(i => i.Obrigatoria,
            "GA-04: itens de grupo são sempre obrigatórios mesmo passando obrigatoria=false");
        revisao.Itens.Should().OnlyContain(i => i.GrupoAlternativaId == grupo);
    }

    // ─── GA-02: grupo precisa de ≥ 2 itens ────────────────────────────────────

    [Fact]
    public void GrupoComUmUnicoItem_LancaInvalidOperationException()
    {
        var grupo = Guid.NewGuid();
        var act = () => GarantiaExigidaRevisao.Criar(
            LimiteId,
            [SpecGrupo(TipoGarantia.CdbCativo, grupo)],
            CriarClock());

        act.Should().Throw<InvalidOperationException>().WithMessage("*GA-02*");
    }

    // ─── GA-03/GA-07: grupo válido com 2 tipos distintos é aceito ─────────────

    [Fact]
    public void GrupoComDoisTiposDistintos_EhAceito()
    {
        var grupo = Guid.NewGuid();
        var revisao = GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupo, 100m),
                SpecGrupo(TipoGarantia.BoletoBancario, grupo, 90m),
            ],
            CriarClock());

        revisao.Itens.Should().HaveCount(2);
        revisao.Itens.Select(i => i.Tipo).Should().BeEquivalentTo(
            [TipoGarantia.CdbCativo, TipoGarantia.BoletoBancario]);
    }

    // ─── GA-07: SR-06 garante que um tipo está em no máximo um grupo ──────────

    [Fact]
    public void TipoDuplicado_AindaEhRejeitadoPorSr06()
    {
        var grupoA = Guid.NewGuid();
        var grupoB = Guid.NewGuid();
        var act = () => GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupoA),
                SpecGrupo(TipoGarantia.BoletoBancario, grupoA),
                SpecGrupo(TipoGarantia.CdbCativo, grupoB), // mesmo tipo em outro grupo
                SpecGrupo(TipoGarantia.Sblc, grupoB),
            ],
            CriarClock());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CdbCativo*", "GA-07/SR-06: um tipo não pode estar em dois grupos");
    }

    // ─── GA-05: rótulo > 120 chars é rejeitado ────────────────────────────────

    [Fact]
    public void RotuloAcimaDe120Caracteres_LancaArgumentException()
    {
        var grupo = Guid.NewGuid();
        string rotuloLongo = new('x', 121);
        var act = () => GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupo, rotulo: rotuloLongo),
                SpecGrupo(TipoGarantia.BoletoBancario, grupo, rotulo: rotuloLongo),
            ],
            CriarClock());

        act.Should().Throw<ArgumentException>().WithMessage("*120*");
    }

    // ─── GA-05: rótulos inconsistentes no mesmo grupo são rejeitados ──────────

    [Fact]
    public void RotulosInconsistentesNoGrupo_LancaInvalidOperationException()
    {
        var grupo = Guid.NewGuid();
        var act = () => GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupo, rotulo: "Rótulo A"),
                SpecGrupo(TipoGarantia.BoletoBancario, grupo, rotulo: "Rótulo B"),
            ],
            CriarClock());

        act.Should().Throw<InvalidOperationException>().WithMessage("*GA-05*");
    }

    // ─── GA-06: campos de grupo permanecem imutáveis após encerramento ────────

    [Fact]
    public void GrupoImutavelAposEncerramento()
    {
        var grupo = Guid.NewGuid();
        var clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(
            LimiteId,
            [
                SpecGrupo(TipoGarantia.CdbCativo, grupo, rotulo: "Colateral FINIMP"),
                SpecGrupo(TipoGarantia.BoletoBancario, grupo, rotulo: "Colateral FINIMP"),
            ],
            clock);

        revisao.EncerrarVigencia(clock.GetCurrentInstant() + Duration.FromDays(1));

        revisao.EstaVigente.Should().BeFalse();
        revisao.Itens.Should().OnlyContain(i =>
            i.GrupoAlternativaId == grupo && i.GrupoRotulo == "Colateral FINIMP",
            "GA-06: campos de grupo preservados na revisão encerrada (snapshot imutável)");
    }
}
