using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes dos métodos de garantias em <see cref="LimiteBanco"/>.
/// Adaptados para semântica de revisões (S34 T0.2/T0.3): cada mutação
/// de garantias fecha a revisão vigente e abre uma nova.
/// </summary>
public sealed class LimiteBancoGarantiasTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static LimiteBanco CriarLimite(IEnumerable<GarantiaExigidaItemSpec>? specs = null) =>
        LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: Clock,
            garantiasExigidas: specs);

    private static GarantiaExigidaItemSpec SpecCdb(decimal percentual = 20m) =>
        new(TipoGarantia.CdbCativo, percentual, null, true, null);

    private static GarantiaExigidaItemSpec SpecAval() =>
        new(TipoGarantia.Aval, null, null, true, null);

    // ─── Criar com garantias ─────────────────────────────────────────────────

    [Fact]
    public void Criar_sem_garantias_resulta_em_colecao_vazia()
    {
        var limite = CriarLimite();
        limite.GarantiasExigidas.Should().BeEmpty();
        limite.RevisaoGarantiasVigente.Should().BeNull();
    }

    [Fact]
    public void Criar_com_uma_garantia_persiste_no_agregado_com_FK_correta()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        limite.GarantiasExigidas.Should().ContainSingle();
        var g = limite.GarantiasExigidas.Single();
        g.Tipo.Should().Be(TipoGarantia.CdbCativo);
        // RevisaoId aponta para a revisão vigente, não para o LimiteBanco
        g.RevisaoId.Should().Be(limite.RevisaoGarantiasVigente!.Id);
    }

    [Fact]
    public void Criar_com_multiplas_garantias_de_tipos_diferentes_funciona()
    {
        var limite = CriarLimite(new[] { SpecCdb(), SpecAval() });

        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.GarantiasExigidas.Select(g => g.Tipo).Should()
            .BeEquivalentTo(new[] { TipoGarantia.CdbCativo, TipoGarantia.Aval });
        // Todos os itens pertencem à revisão vigente
        var revisaoId = limite.RevisaoGarantiasVigente!.Id;
        limite.GarantiasExigidas.Should().OnlyContain(g => g.RevisaoId == revisaoId);
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
        limite.RevisoesGarantiasExigidas.Should().HaveCount(1);
    }

    [Fact]
    public void Adicionar_garantia_em_limite_com_revisao_vigente_cria_nova_revisao()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        var clockDepois = PropostaFactory.CriarClockFixo(2026, 6, 1);
        limite.AdicionarGarantiaExigida(SpecAval(), clockDepois);

        // Deve haver 2 revisões: a original (encerrada) e a nova (vigente com 2 itens)
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.RevisaoGarantiasVigente.Should().NotBeNull();
        limite.RevisaoGarantiasVigente!.VigenciaFim.Should().BeNull();
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

    // ─── RemoverGarantiaExigidaPorTipo ──────────────────────────────────────

    [Fact]
    public void Remover_garantia_por_tipo_funciona()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        limite.RemoverGarantiaExigidaPorTipo(TipoGarantia.CdbCativo, Clock);

        // Nova revisão criada com lista vazia
        limite.GarantiasExigidas.Should().BeEmpty();
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
    }

    [Fact]
    public void Remover_garantia_tipo_inexistente_deve_lancar_excecao()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        var act = () => limite.RemoverGarantiaExigidaPorTipo(TipoGarantia.Aval, Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*não encontrada*");
    }

    [Fact]
    public void Remover_garantia_sem_revisao_vigente_deve_lancar_excecao()
    {
        var limite = CriarLimite(); // sem garantias = sem revisão

        var act = () => limite.RemoverGarantiaExigidaPorTipo(TipoGarantia.CdbCativo, Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nenhuma revisão vigente*");
    }

    // ─── SubstituirGarantiasExigidas ─────────────────────────────────────────

    [Fact]
    public void Substituir_garantias_remove_anteriores_e_adiciona_novas()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        var novas = new[]
        {
            new GarantiaExigidaItemSpec(TipoGarantia.Sblc, 50m, null, true, null),
            new GarantiaExigidaItemSpec(TipoGarantia.Fgi, 30m, null, false, null),
        };

        limite.SubstituirGarantiasExigidas(novas, Clock);

        // GarantiasExigidas retorna itens da revisão vigente (nova)
        limite.GarantiasExigidas.Should().HaveCount(2);
        limite.GarantiasExigidas.Select(g => g.Tipo).Should()
            .BeEquivalentTo(new[] { TipoGarantia.Sblc, TipoGarantia.Fgi });
        // Deve haver 2 revisões: original (fechada) + nova
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
    }

    [Fact]
    public void Substituir_com_colecao_vazia_cria_nova_revisao_vazia()
    {
        var limite = CriarLimite(new[] { SpecCdb() });

        limite.SubstituirGarantiasExigidas(Array.Empty<GarantiaExigidaItemSpec>(), Clock);

        limite.GarantiasExigidas.Should().BeEmpty();
        // Lista diferente → nova revisão criada
        limite.RevisoesGarantiasExigidas.Should().HaveCount(2);
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
