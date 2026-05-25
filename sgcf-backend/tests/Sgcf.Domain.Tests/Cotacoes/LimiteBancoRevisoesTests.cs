using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes unitários de revisões de garantias em <see cref="LimiteBanco"/>.
/// Cobre invariantes SLB-01..SLB-05 (SPEC §4.2).
/// </summary>
public sealed class LimiteBancoRevisoesTests
{
    private static readonly Guid BancoId = Guid.NewGuid();

    private static IClock CriarClock(int ano = 2026, int mes = 1, int dia = 1) =>
        PropostaFactory.CriarClockFixo(ano, mes, dia);

    private static LimiteBanco CriarLimite(
        IClock? clock = null,
        IEnumerable<GarantiaExigidaItemSpec>? garantias = null) =>
        LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock ?? CriarClock(),
            garantiasExigidas: garantias);

    private static GarantiaExigidaItemSpec SpecCdb(decimal percentual = 20m) =>
        new(TipoGarantia.CdbCativo, percentual, null, true, null);

    private static GarantiaExigidaItemSpec SpecAval() =>
        new(TipoGarantia.Aval, null, null, true, null);

    private static GarantiaExigidaItemSpec SpecFgi(decimal percentual = 10m) =>
        new(TipoGarantia.Fgi, percentual, null, false, null);

    // ─── SLB-01: No máximo uma revisão vigente por LimiteBanco ───────────────

    [Fact]
    public void RevisaoGarantiasVigente_SempreUmaPorLimite()
    {
        // SLB-01: após múltiplas substituições, apenas 1 revisão tem VigenciaFim null
        var clock = CriarClock();
        var limite = CriarLimite(clock, new[] { SpecCdb() });

        var clock2 = CriarClock(2026, 3, 1);
        limite.SubstituirGarantiasExigidas(new[] { SpecAval() }, clock2);

        var clock3 = CriarClock(2026, 6, 1);
        limite.SubstituirGarantiasExigidas(new[] { SpecCdb(30m) }, clock3);

        var revisoesVigentes = limite.RevisoesGarantiasExigidas
            .Where(r => r.VigenciaFim is null)
            .ToList();

        revisoesVigentes.Should().HaveCount(1);
        limite.RevisaoGarantiasVigente.Should().NotBeNull();
    }

    // ─── SLB-02: Primeira SubstituirGarantias cria revisão inicial ───────────

    [Fact]
    public void SubstituirGarantiasExigidas_PrimeiraVez_CriaRevisaoInicial()
    {
        // SLB-02: limite sem garantias → SubstituirGarantiasExigidas → cria 1ª revisão
        var clock = CriarClock();
        var limite = CriarLimite(clock); // sem garantias

        limite.SubstituirGarantiasExigidas(new[] { SpecCdb() }, clock);

        limite.RevisoesGarantiasExigidas.Should().HaveCount(1);
        limite.RevisaoGarantiasVigente.Should().NotBeNull();
        limite.GarantiasExigidas.Should().ContainSingle(g => g.Tipo == TipoGarantia.CdbCativo);
    }

    // ─── SLB-02+SLB-03: Fecha vigente e abre nova com mesmo Instant ──────────

    [Fact]
    public void SubstituirGarantiasExigidas_ListaDiferente_FechaVigenteEAbreNovaNoMesmoInstant()
    {
        // SLB-02 + SLB-03: VigenciaFim da anterior == VigenciaInicio da nova
        var clockInicial = CriarClock(2026, 1, 1);
        var clockPatch = CriarClock(2026, 6, 1);

        var limite = CriarLimite(clockInicial, new[] { SpecCdb() });
        var revisaoOriginal = limite.RevisaoGarantiasVigente!;

        limite.SubstituirGarantiasExigidas(new[] { SpecAval() }, clockPatch, motivo: "Renegociação");

        var revisaoNova = limite.RevisaoGarantiasVigente!;

        // SLB-02: revisão original encerrada
        revisaoOriginal.VigenciaFim.Should().NotBeNull();
        // SLB-03: continuidade temporal — sem gap
        revisaoOriginal.VigenciaFim.Should().Be(revisaoNova.VigenciaInicio);
        revisaoNova.Motivo.Should().Be("Renegociação");
        revisaoNova.Itens.Should().ContainSingle(i => i.Tipo == TipoGarantia.Aval);
    }

    // ─── SLB-04: Lista equivalente não cria nova revisão (idempotência) ───────

    [Fact]
    public void SubstituirGarantiasExigidas_ListaEquivalente_NaoCriaNovaRevisao()
    {
        // SLB-04: mesmos tipos, percentuais, obrigatoriedade e observações
        var clock = CriarClock();
        var limite = CriarLimite(clock, new[] { SpecCdb(20m) });

        int countAntes = limite.RevisoesGarantiasExigidas.Count;

        // Mesma spec — deve ser idempotente
        limite.SubstituirGarantiasExigidas(new[] { SpecCdb(20m) }, clock);

        limite.RevisoesGarantiasExigidas.Should().HaveCount(countAntes);
    }

    [Fact]
    public void SubstituirGarantiasExigidas_MesmaListaEmOrdemDiferente_NaoCriaNovaRevisao()
    {
        // SLB-04 variante: ordem dos itens não altera equivalência
        var clock = CriarClock();
        var limite = CriarLimite(clock, new[] { SpecCdb(), SpecAval() });

        int countAntes = limite.RevisoesGarantiasExigidas.Count;

        // Mesmos itens em ordem invertida
        limite.SubstituirGarantiasExigidas(new[] { SpecAval(), SpecCdb() }, clock);

        limite.RevisoesGarantiasExigidas.Should().HaveCount(countAntes);
    }

    [Fact]
    public void SubstituirGarantiasExigidas_ListaComPercentualDiferente_CriaNovaRevisao()
    {
        // SLB-04: percentual diferente → não equivalente → nova revisão
        var clock = CriarClock();
        var limite = CriarLimite(clock, new[] { SpecCdb(20m) });

        limite.SubstituirGarantiasExigidas(new[] { SpecCdb(30m) }, clock);

        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
    }

    // ─── SLB-05: Histórico ordenável por VigenciaInicio ─────────────────────

    [Fact]
    public void RevisoesGarantiasExigidas_OrdemNaoGarantida_QueryOrdenaPorVigenciaInicio()
    {
        // SLB-05: a coleção em memória não garante ordem, mas podemos ordenar na query
        var clock1 = CriarClock(2026, 1, 1);
        var clock2 = CriarClock(2026, 3, 1);
        var clock3 = CriarClock(2026, 6, 1);

        var limite = CriarLimite(clock1, new[] { SpecCdb() });
        limite.SubstituirGarantiasExigidas(new[] { SpecAval() }, clock2);
        limite.SubstituirGarantiasExigidas(new[] { SpecFgi() }, clock3);

        var revisoesOrdenadas = limite.RevisoesGarantiasExigidas
            .OrderBy(r => r.VigenciaInicio)
            .ToList();

        revisoesOrdenadas.Should().HaveCount(3);
        revisoesOrdenadas[0].VigenciaInicio.Should().Be(clock1.GetCurrentInstant());
        revisoesOrdenadas[1].VigenciaInicio.Should().Be(clock2.GetCurrentInstant());
        revisoesOrdenadas[2].VigenciaInicio.Should().Be(clock3.GetCurrentInstant());
        // Apenas a última deve estar vigente
        revisoesOrdenadas[0].VigenciaFim.Should().NotBeNull();
        revisoesOrdenadas[1].VigenciaFim.Should().NotBeNull();
        revisoesOrdenadas[2].VigenciaFim.Should().BeNull();
    }

    // ─── AdicionarGarantiaExigida via revisão ────────────────────────────────

    [Fact]
    public void AdicionarGarantiaExigida_PrimeiraVez_CriaRevisaoComUmItem()
    {
        var clock = CriarClock();
        var limite = CriarLimite(clock); // sem garantias = sem revisão

        limite.AdicionarGarantiaExigida(SpecCdb(), clock);

        limite.RevisoesGarantiasExigidas.Should().HaveCount(1);
        limite.GarantiasExigidas.Should().ContainSingle(g => g.Tipo == TipoGarantia.CdbCativo);
        limite.RevisaoGarantiasVigente.Should().NotBeNull();
    }

    [Fact]
    public void AdicionarGarantiaExigida_ComRevisaoVigente_FechaEAbreNovaComItemAdicional()
    {
        var clock1 = CriarClock(2026, 1, 1);
        var clock2 = CriarClock(2026, 3, 1);

        var limite = CriarLimite(clock1, new[] { SpecCdb() });
        var revisaoOriginal = limite.RevisaoGarantiasVigente!;

        limite.AdicionarGarantiaExigida(SpecAval(), clock2);

        // Revisão original encerrada; nova com 2 itens
        revisaoOriginal.EstaVigente.Should().BeFalse();
        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
    }

    // ─── RemoverGarantiaExigidaPorTipo via revisão ───────────────────────────

    [Fact]
    public void RemoverGarantiaExigidaPorTipo_FechaVigenteEAbreNovaSemAquelaGarantia()
    {
        var clock1 = CriarClock(2026, 1, 1);
        var clock2 = CriarClock(2026, 6, 1);

        var limite = CriarLimite(clock1, new[] { SpecCdb(), SpecAval() });

        limite.RemoverGarantiaExigidaPorTipo(TipoGarantia.CdbCativo, clock2);

        limite.GarantiasExigidas.Should().ContainSingle(g => g.Tipo == TipoGarantia.Aval);
        limite.GarantiasExigidas.Should().NotContain(g => g.Tipo == TipoGarantia.CdbCativo);
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
    }

    // ─── Compatibilidade: GarantiasExigidas retorna itens da revisão vigente ─

    [Fact]
    public void GarantiasExigidas_SemRevisao_RetornaColecaoVazia()
    {
        var limite = CriarLimite(); // sem garantias
        limite.GarantiasExigidas.Should().BeEmpty();
    }

    [Fact]
    public void GarantiasExigidas_RetornaSempreItensDoRevisaoVigenteApos_Substituicoes()
    {
        var clock1 = CriarClock(2026, 1, 1);
        var clock2 = CriarClock(2026, 6, 1);

        var limite = CriarLimite(clock1, new[] { SpecCdb(20m) });

        // Após substituição, GarantiasExigidas deve refletir a nova revisão
        limite.SubstituirGarantiasExigidas(new[] { SpecFgi(15m) }, clock2);

        limite.GarantiasExigidas.Should().ContainSingle(g =>
            g.Tipo == TipoGarantia.Fgi && g.PercentualSobreLimite == 15m);
    }
}
