using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários de <see cref="AvaliadorCoberturaGarantia.Avaliar"/>.
///
/// Cenários cobertos (valorPrincipalBrl = 100 000 BRL,
/// grupo "CdbCativo 100% OU BoletoBancario 100%"):
/// <list type="bullet">
///   <item>AC-1: 100k Cdb → Σ=1,0 → sem lacuna.</item>
///   <item>AC-2: 100k Boleto → Σ=1,0 → sem lacuna.</item>
///   <item>AC-3: 60k Cdb + 40k Boleto → Σ=1,0 → sem lacuna.</item>
///   <item>AC-4: 50k Cdb + 40k Boleto → Σ=0,9 → 1 lacuna de grupo.</item>
///   <item>AC-5: grupo coberto (100k Cdb) + Aval puro não declarado → 1 lacuna de item.</item>
///   <item>AC-6: nada declarado → lacuna de grupo (Σ=0).</item>
///   <item>Regressão: item independente CdbCativo 50% com 40k → lacuna esperado=50k coberto=40k.</item>
/// </list>
/// </summary>
[Trait("Category", "Unit")]
public sealed class AvaliadorCoberturaGarantiaTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();
    private static readonly Money ValorPrincipal = new(100_000m, Moeda.Brl);

    // ── Grupo: CdbCativo 100% OU BoletoBancario 100% ─────────────────────────

    private static readonly Guid GrupoId = Guid.NewGuid();
    private const string GrupoRotuloTeste = "Colateral mínimo";

    private static List<GarantiaExigidaItem> CriarItensGrupo(string? rotulo = GrupoRotuloTeste)
    {
        IClock clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens:
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 100m, null, true, null, GrupoId, rotulo),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario, 100m, null, true, null, GrupoId, rotulo),
            ],
            clock: clock);

        return revisao.Itens.Where(i => i.Obrigatoria).ToList();
    }

    private static List<GarantiaExigidaItem> CriarItensGrupoMaisAvalPuro()
    {
        IClock clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens:
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 100m, null, true, null, GrupoId, GrupoRotuloTeste),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario, 100m, null, true, null, GrupoId, GrupoRotuloTeste),
                // Aval puro independente (sem grupo)
                new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, true, null),
            ],
            clock: clock);

        return revisao.Itens.Where(i => i.Obrigatoria).ToList();
    }

    private static IClock CriarClock() =>
        TestHelpers.CriarClock();

    private static Dictionary<TipoGarantia, decimal> Cobertura(
        params (TipoGarantia tipo, decimal valor)[] entradas)
        => entradas.ToDictionary(e => e.tipo, e => e.valor);

    // ── AC-1: 100k Cdb apenas → Σ=1,0 → sem lacuna ──────────────────────────

    [Fact]
    public void Avaliar_GrupoCdbCoberto100k_SemLacuna()
    {
        var itens = CriarItensGrupo();
        var cobertura = Cobertura((TipoGarantia.CdbCativo, 100_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().BeEmpty();
    }

    // ── AC-2: 100k Boleto apenas → Σ=1,0 → sem lacuna ───────────────────────

    [Fact]
    public void Avaliar_GrupoBoletoCobert100k_SemLacuna()
    {
        var itens = CriarItensGrupo();
        var cobertura = Cobertura((TipoGarantia.BoletoBancario, 100_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().BeEmpty();
    }

    // ── AC-3: 60k Cdb + 40k Boleto → 0,6+0,4=1,0 → sem lacuna ─────────────

    [Fact]
    public void Avaliar_GrupoCdb60kBoleto40k_SommaExataUm_SemLacuna()
    {
        var itens = CriarItensGrupo();
        var cobertura = Cobertura(
            (TipoGarantia.CdbCativo, 60_000m),
            (TipoGarantia.BoletoBancario, 40_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().BeEmpty();
    }

    // ── AC-4: 50k Cdb + 40k Boleto → 0,5+0,4=0,9 → 1 lacuna de grupo ───────

    [Fact]
    public void Avaliar_GrupoCdb50kBoleto40k_FracaoInsuficiente_UmaLacunaDeGrupo()
    {
        var itens = CriarItensGrupo();
        var cobertura = Cobertura(
            (TipoGarantia.CdbCativo, 50_000m),
            (TipoGarantia.BoletoBancario, 40_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().HaveCount(1);

        LacunaGarantia lacuna = resultado[0];
        lacuna.Obrigatoria.Should().BeTrue();
        lacuna.GrupoAlternativaId.Should().Be(GrupoId);
        lacuna.GrupoRotulo.Should().Be(GrupoRotuloTeste);
        lacuna.FracaoCoberta.Should().Be(0.9m);
        lacuna.AlternativasAceitas.Should().BeEquivalentTo(
            new[] { nameof(TipoGarantia.CdbCativo), nameof(TipoGarantia.BoletoBancario) },
            options => options.WithoutStrictOrdering());
        lacuna.ValorEsperadoBrl.Should().BeNull();
        lacuna.ValorCobertoBrl.Should().BeNull();
    }

    // ── AC-5: grupo coberto + Aval puro não declarado → 1 lacuna de item ─────

    [Fact]
    public void Avaliar_GrupoCoberto_AvalPuroNaoDeclarado_UmaLacunaDeItemAval()
    {
        var itens = CriarItensGrupoMaisAvalPuro();
        // Cobre o grupo totalmente com CDB, mas não declara Aval.
        var cobertura = Cobertura((TipoGarantia.CdbCativo, 100_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().HaveCount(1);

        LacunaGarantia lacuna = resultado[0];
        lacuna.Tipo.Should().Be(nameof(TipoGarantia.Aval));
        lacuna.GrupoAlternativaId.Should().BeNull("item Aval é independente (não agrupado)");
        lacuna.ValorEsperadoBrl.Should().BeNull("Aval puro não tem valor monetário");
    }

    // ── AC-6: nada declarado → lacuna de grupo (Σ=0) ─────────────────────────

    [Fact]
    public void Avaliar_NadaDeclarado_GrupoComFracaoZero_UmaLacuna()
    {
        var itens = CriarItensGrupo();
        var cobertura = Cobertura(); // dicionário vazio

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().HaveCount(1);

        LacunaGarantia lacuna = resultado[0];
        lacuna.FracaoCoberta.Should().Be(0m);
        lacuna.GrupoAlternativaId.Should().Be(GrupoId);
    }

    // ── Regressão: item independente CdbCativo 50% com 40k → lacuna ──────────

    [Fact]
    public void Avaliar_ItemIndependenteCdb50PctCoberto40k_LacunaComValores()
    {
        IClock clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens: [new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 50m, null, true, null)],
            clock: clock);

        var itens = revisao.Itens.Where(i => i.Obrigatoria).ToList();
        var cobertura = Cobertura((TipoGarantia.CdbCativo, 40_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().HaveCount(1);

        LacunaGarantia lacuna = resultado[0];
        lacuna.Tipo.Should().Be(nameof(TipoGarantia.CdbCativo));
        lacuna.ValorEsperadoBrl.Should().Be(50_000m); // 50% de 100k
        lacuna.ValorCobertoBrl.Should().Be(40_000m);
        lacuna.GrupoAlternativaId.Should().BeNull();
        lacuna.FracaoCoberta.Should().BeNull();
    }

    // ── Rótulo do grupo: quando ausente usa formato "Grupo: A OU B" ──────────

    [Fact]
    public void Avaliar_GrupoSemRotuloNaoDeclarado_TipoUsaFormatoGrupo()
    {
        var itens = CriarItensGrupo(rotulo: null); // sem rótulo
        var cobertura = Cobertura(); // nada declarado

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().HaveCount(1);
        resultado[0].Tipo.Should().StartWith("Grupo:");
        resultado[0].GrupoRotulo.Should().BeNull();
    }

    // ── Regressão (review): dízima exata não pode falso-bloquear ─────────────
    //
    // Grupo de 3 alternativas, cada uma com alvo 30% de 100k = 30k. Cobrindo 10k de
    // cada → fração 1/3 por alternativa → soma decimal = 0,999…9 (28 noves). Sem o
    // arredondamento da soma isso bloquearia indevidamente um grupo que está coberto.

    private static List<GarantiaExigidaItem> CriarItensGrupoTres()
    {
        IClock clock = CriarClock();
        var revisao = GarantiaExigidaRevisao.Criar(
            limiteBancoId: LimiteId,
            itens:
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, 30m, null, true, null, GrupoId, GrupoRotuloTeste),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario, 30m, null, true, null, GrupoId, GrupoRotuloTeste),
                new GarantiaExigidaItemSpec(TipoGarantia.Duplicatas, 30m, null, true, null, GrupoId, GrupoRotuloTeste),
            ],
            clock: clock);

        return revisao.Itens.Where(i => i.Obrigatoria).ToList();
    }

    [Fact]
    public void Avaliar_GrupoTresAlternativasCobertasEmTercos_NaoFalsoBloqueia()
    {
        var itens = CriarItensGrupoTres();
        // alvo de cada alternativa = 30% de 100k = 30k; 10k/30k = 1/3 cada → soma = 1,0.
        var cobertura = Cobertura(
            (TipoGarantia.CdbCativo, 10_000m),
            (TipoGarantia.BoletoBancario, 10_000m),
            (TipoGarantia.Duplicatas, 10_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().BeEmpty(
            "1/3 + 1/3 + 1/3 cobre o grupo; dízima decimal não pode bloquear");
    }

    // ── Cap de over-coverage: uma alternativa acima de 100% satisfaz o grupo ──
    //
    // min(coberto/alvo, 1.0) impede que uma alternativa super-coberta "transborde"
    // para mascarar outra; mas uma alternativa sozinha cobrindo ≥100% do seu alvo
    // satisfaz o grupo (Σ = 1,0). É a semântica "OU" pretendida.

    [Fact]
    public void Avaliar_GrupoUmaAlternativaAcimaDe100Pct_SatisfazGrupo()
    {
        var itens = CriarItensGrupo(); // CdbCativo 100% OU BoletoBancario 100% (alvo 100k cada)
        // Cdb cobre 150k (min(1.5,1.0)=1.0); Boleto zero. Σ = 1,0 → coberto.
        var cobertura = Cobertura((TipoGarantia.CdbCativo, 150_000m));

        List<LacunaGarantia> resultado = AvaliadorCoberturaGarantia.Avaliar(itens, cobertura, ValorPrincipal);

        resultado.Should().BeEmpty(
            "uma alternativa cobrindo >=100% do próprio alvo satisfaz o grupo OU");
    }
}
