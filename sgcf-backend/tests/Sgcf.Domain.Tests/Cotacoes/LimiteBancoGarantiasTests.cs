using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class LimiteBancoGarantiasTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static LimiteBanco CriarLimite(IEnumerable<GarantiaExigidaLimiteSpec>? specs = null) =>
        LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: Clock,
            garantiasExigidas: specs);

    private static GarantiaExigidaLimiteSpec SpecCdb(decimal percentual = 20m) =>
        new(TipoGarantia.CdbCativo, percentual, null, true, null);

    private static GarantiaExigidaLimiteSpec SpecAval() =>
        new(TipoGarantia.Aval, null, null, true, null);

    // ─── Criar com garantias ─────────────────────────────────────────────────

    [Fact]
    public void Criar_sem_garantias_resulta_em_colecao_vazia()
    {
        var limite = CriarLimite();
        limite.GarantiasExigidas.Should().BeEmpty();
    }

    [Fact]
    public void Criar_com_uma_garantia_persiste_no_agregado_com_FK_correta()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        limite.GarantiasExigidas.Should().ContainSingle();
        var g = limite.GarantiasExigidas.Single();
        g.Tipo.Should().Be(TipoGarantia.CdbCativo);
        g.LimiteBancoId.Should().Be(limite.Id);
    }

    [Fact]
    public void Criar_com_multiplas_garantias_de_tipos_diferentes_funciona()
    {
        var limite = CriarLimite(new[] { SpecCdb(), SpecAval() });

        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.GarantiasExigidas.Select(g => g.Tipo).Should()
            .BeEquivalentTo(new[] { TipoGarantia.CdbCativo, TipoGarantia.Aval });
        limite.GarantiasExigidas.Should().OnlyContain(g => g.LimiteBancoId == limite.Id);
    }

    [Fact]
    public void Criar_com_garantias_duplicadas_por_tipo_deve_lancar_excecao()
    {
        var act = () => CriarLimite(new[]
        {
            SpecCdb(20m),
            SpecCdb(10m),
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicada*");
    }

    // ─── AdicionarGarantiaExigida ────────────────────────────────────────────

    [Fact]
    public void Adicionar_garantia_em_limite_sem_garantias_deve_funcionar()
    {
        var limite = CriarLimite();

        limite.AdicionarGarantiaExigida(SpecCdb(), Clock);

        limite.GarantiasExigidas.Should().ContainSingle();
    }

    [Fact]
    public void Adicionar_garantia_com_tipo_ja_presente_deve_lancar_excecao()
    {
        var limite = CriarLimite(new[] { SpecCdb(20m) });

        var act = () => limite.AdicionarGarantiaExigida(SpecCdb(10m), Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicada*");
    }

    [Fact]
    public void Adicionar_garantia_atualiza_UpdatedAt_do_limite()
    {
        var limite = CriarLimite();
        var clockDepois = PropostaFactory.CriarClockFixo(2026, 6, 1);

        limite.AdicionarGarantiaExigida(SpecCdb(), clockDepois);

        limite.UpdatedAt.Should().Be(clockDepois.GetCurrentInstant());
    }

    // ─── RemoverGarantiaExigida ──────────────────────────────────────────────

    [Fact]
    public void Remover_garantia_por_id_funciona()
    {
        var limite = CriarLimite(new[] { SpecCdb() });
        var id = limite.GarantiasExigidas.Single().Id;

        limite.RemoverGarantiaExigida(id, Clock);

        limite.GarantiasExigidas.Should().BeEmpty();
    }

    [Fact]
    public void Remover_garantia_inexistente_deve_lancar_excecao()
    {
        var limite = CriarLimite();

        var act = () => limite.RemoverGarantiaExigida(Guid.NewGuid(), Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    // ─── SubstituirGarantiasExigidas ─────────────────────────────────────────

    [Fact]
    public void Substituir_garantias_remove_anteriores_e_adiciona_novas()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        var novas = new[]
        {
            new GarantiaExigidaLimiteSpec(TipoGarantia.Sblc, 50m, null, true, null),
            new GarantiaExigidaLimiteSpec(TipoGarantia.Fgi, 30m, null, false, null),
        };

        limite.SubstituirGarantiasExigidas(novas, Clock);

        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.GarantiasExigidas.Select(g => g.Tipo).Should()
            .BeEquivalentTo(new[] { TipoGarantia.Sblc, TipoGarantia.Fgi });
    }

    [Fact]
    public void Substituir_com_colecao_vazia_limpa_todas()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        limite.SubstituirGarantiasExigidas(Array.Empty<GarantiaExigidaLimiteSpec>(), Clock);

        limite.GarantiasExigidas.Should().BeEmpty();
    }

    [Fact]
    public void Substituir_com_duplicados_por_tipo_deve_lancar_excecao()
    {
        var limite = CriarLimite();

        var duplicadas = new[]
        {
            SpecCdb(20m),
            SpecCdb(10m),
        };

        var act = () => limite.SubstituirGarantiasExigidas(duplicadas, Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicada*");
    }
}
