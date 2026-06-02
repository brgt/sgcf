using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários de <see cref="CalculadorValorGarantiaExigida"/>.
/// Cobre: coleção vazia, percentual puro, valor fixo puro, mix, Aval sem contribuição,
/// e grupos de alternativas "OU" (RV-GA): mínimo por grupo, invariância com grupo unitário,
/// mix de itens independentes com grupos.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CalculadorValorGarantiaExigidaTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();
    private static readonly Money ValorAlvo = new(1_000_000m, Moeda.Brl);

    private static readonly IClock Clock = CriarClock();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 16, 9, 0));
        return clock;
    }

    private static GarantiaExigidaItem CriarGarantia(
        TipoGarantia tipo,
        decimal? percentual = null,
        decimal? valorFixo = null,
        bool obrigatoria = true) =>
        GarantiaExigidaItem.Criar(
            revisaoId: LimiteId,
            tipo: tipo,
            percentualSobreLimite: percentual,
            valorFixoBrl: valorFixo.HasValue ? new Money(valorFixo.Value, Moeda.Brl) : null,
            obrigatoria: obrigatoria,
            observacoes: null,
            clock: Clock);

    // ─── Coleção vazia ────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ColecaoVazia_RetornaZero()
    {
        Money resultado = CalculadorValorGarantiaExigida.Calcular(
            Array.Empty<GarantiaExigidaItem>(), ValorAlvo);

        resultado.Valor.Should().Be(0m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Percentual puro ──────────────────────────────────────────────────────

    [Fact]
    public void Calcular_GarantiaComPercentual20PctSobreUmMilhao_Retorna200k()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(200_000m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Valor fixo puro ──────────────────────────────────────────────────────

    [Fact]
    public void Calcular_GarantiaComValorFixo50k_Retorna50k()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.Sblc, valorFixo: 50_000m);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(50_000m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Mix: percentual + valor fixo ─────────────────────────────────────────

    [Fact]
    public void Calcular_CdbCativo20PctMaisSblc50k_RetornaSoma()
    {
        // 20% de 1.000.000 = 200.000 + 50.000 = 250.000
        IReadOnlyCollection<GarantiaExigidaItem> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m),
            CriarGarantia(TipoGarantia.Sblc,      valorFixo:  50_000m)
        ];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(garantias, ValorAlvo);

        resultado.Valor.Should().Be(250_000m);
    }

    // ─── Aval não contribui com valor ────────────────────────────────────────

    [Fact]
    public void Calcular_ApenasAval_RetornaZero()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.Aval);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(0m);
    }

    [Fact]
    public void Calcular_CdbCativo20PctMaisAval_IgnoraContribuicaoAval()
    {
        // Aval não adiciona valor; total deve ser apenas 20% do valorAlvo
        IReadOnlyCollection<GarantiaExigidaItem> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m),
            CriarGarantia(TipoGarantia.Aval)
        ];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(garantias, ValorAlvo);

        resultado.Valor.Should().Be(200_000m);
    }

    // ─── Moeda errada → exceção clara ─────────────────────────────────────────

    [Fact]
    public void Calcular_ValorAlvoNaoBrl_LancaArgumentException()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m);
        Money valorUsd = new(100_000m, Moeda.Usd);

        Action act = () => CalculadorValorGarantiaExigida.Calcular([garantia], valorUsd);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ─── Grupos de alternativas "OU" (RV-GA) ─────────────────────────────────
    //
    // Usa GarantiaExigidaRevisao.Criar para garantir que os itens passem pelas
    // invariantes de domínio (GA-01..GA-07) — em especial GA-02 (≥2 por grupo).

    private static Guid LimiteIdParaGrupo { get; } = Guid.NewGuid();

    /// <summary>
    /// Grupo de 2 alternativas: CdbCativo 100% e BoletoBancario 80%.
    /// Sobre valorAlvo = 100 000 BRL → contribuições individuais 100k e 80k.
    /// O grupo deve contribuir com min(100k, 80k) = 80k, NÃO com 180k.
    /// </summary>
    [Fact]
    public void Calcular_GrupoDe2_ContribuiComMinimo()
    {
        Money valorAlvo100k = new(100_000m, Moeda.Brl);
        Guid grupoId = Guid.NewGuid();

        GarantiaExigidaRevisao revisao = GarantiaExigidaRevisao.Criar(
            LimiteIdParaGrupo,
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo,     100m, null, false, null, grupoId, "Colateral FINIMP"),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario,  80m, null, false, null, grupoId, "Colateral FINIMP"),
            ],
            Clock);

        Money resultado = CalculadorValorGarantiaExigida.Calcular(revisao.Itens, valorAlvo100k);

        // 100% de 100k = 100k; 80% de 100k = 80k → min = 80k
        resultado.Valor.Should().Be(80_000m,
            "o grupo contribui com o mínimo entre as alternativas, não com a soma");
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    /// <summary>
    /// Invariante de preservação: um único item agrupado (grupo de um) deve contribuir
    /// exatamente com seu próprio valor. Confirma que min({v}) = v para grupos unitários.
    /// Nota: GA-02 exige ≥2 membros no agregado — aqui construímos dois itens no mesmo
    /// grupo mas passamos apenas um para o calculador, testando somente a lógica de min.
    /// </summary>
    [Fact]
    public void Calcular_GrupoComUmItem_ContribuiComSeuProprioValor()
    {
        Money valorAlvo100k = new(100_000m, Moeda.Brl);
        Guid grupoId = Guid.NewGuid();

        // Construímos a revisão com os 2 obrigatórios para satisfazer GA-02,
        // depois filtramos para entregar apenas um ao calculador.
        GarantiaExigidaRevisao revisao = GarantiaExigidaRevisao.Criar(
            LimiteIdParaGrupo,
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo,     50m, null, false, null, grupoId, "Colateral"),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario, 80m, null, false, null, grupoId, "Colateral"),
            ],
            Clock);

        // Apenas o CdbCativo (50%) é passado ao calculador — grupo de um membro.
        IReadOnlyCollection<GarantiaExigidaItem> apenasUm =
            revisao.Itens.Where(i => i.Tipo == TipoGarantia.CdbCativo).ToList().AsReadOnly();

        Money resultado = CalculadorValorGarantiaExigida.Calcular(apenasUm, valorAlvo100k);

        // min({50k}) = 50k
        resultado.Valor.Should().Be(50_000m,
            "grupo de um membro contribui com o seu próprio valor (invariante de preservação)");
    }

    /// <summary>
    /// Mix: um item independente (Sblc fixo 50k) mais um grupo "OU" de 2 alternativas.
    /// O independente soma normalmente; o grupo contribui apenas com seu mínimo.
    /// </summary>
    [Fact]
    public void Calcular_ItemIndependenteMaisGrupo_SomaIndependenteMaisMinDoGrupo()
    {
        Money valorAlvo100k = new(100_000m, Moeda.Brl);
        Guid grupoId = Guid.NewGuid();
        Guid limiteId = Guid.NewGuid();

        // Item independente: Sblc valor fixo 50k
        GarantiaExigidaItem sblcIndependente = GarantiaExigidaItem.Criar(
            revisaoId: limiteId,
            tipo: TipoGarantia.Sblc,
            percentualSobreLimite: null,
            valorFixoBrl: new Money(50_000m, Moeda.Brl),
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        // Grupo: CdbCativo 100% (100k) e BoletoBancario 80% (80k) — min = 80k
        GarantiaExigidaRevisao revisao = GarantiaExigidaRevisao.Criar(
            LimiteIdParaGrupo,
            [
                new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo,     100m, null, false, null, grupoId, "Colateral FINIMP"),
                new GarantiaExigidaItemSpec(TipoGarantia.BoletoBancario,  80m, null, false, null, grupoId, "Colateral FINIMP"),
            ],
            Clock);

        List<GarantiaExigidaItem> todos = [sblcIndependente, .. revisao.Itens];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(todos.AsReadOnly(), valorAlvo100k);

        // 50k (Sblc fixo) + 80k (min do grupo) = 130k
        resultado.Valor.Should().Be(130_000m,
            "independente soma normalmente; grupo contribui apenas com o mínimo entre suas alternativas");
    }

    /// <summary>
    /// Regressão: itens independentes (sem grupo) continuam somando normalmente.
    /// Garante que a nova lógica de grupos não afeta o caminho legado.
    /// </summary>
    [Fact]
    public void Calcular_ItensSemGrupo_ComportamentoLegadoPreservado()
    {
        // 20% de 1.000.000 = 200.000 + 50.000 = 250.000 (regressão do teste CdbCativo20PctMaisSblc50k)
        IReadOnlyCollection<GarantiaExigidaItem> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m),
            CriarGarantia(TipoGarantia.Sblc,      valorFixo:  50_000m),
        ];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(garantias, ValorAlvo);

        resultado.Valor.Should().Be(250_000m,
            "itens sem grupo continuam somando individualmente (comportamento legado inalterado)");
        resultado.Moeda.Should().Be(Moeda.Brl);
    }
}
