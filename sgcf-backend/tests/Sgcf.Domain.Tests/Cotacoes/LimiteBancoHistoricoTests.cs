using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class LimiteBancoHistoricoTests
{
    private static readonly Guid BancoId = Guid.NewGuid();

    private static LimiteBanco CriarLimite(IClock clock, decimal valor = 10_000_000m) =>
        LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valor, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock);

    // ─── Histórico inicial ───────────────────────────────────────────────────

    [Fact]
    public void Criar_limite_registra_entrada_inicial_no_historico()
    {
        var clock = PropostaFactory.CriarClockFixo(2026, 1, 1);

        var limite = CriarLimite(clock, valor: 5_000_000m);

        limite.Historico.Should().ContainSingle();
        var entry = limite.Historico.Single();
        entry.ValorAnteriorBrl.Should().BeNull();
        entry.ValorNovoBrl.Valor.Should().Be(5_000_000m);
        entry.RegistradoEm.Should().Be(clock.GetCurrentInstant());
    }

    // ─── Atualizar valor registra histórico ──────────────────────────────────

    [Fact]
    public void Atualizar_valor_do_limite_acrescenta_entrada_no_historico()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = CriarLimite(clockCriacao, valor: 5_000_000m);
        limite.Atualizar(clockUpdate, novoLimiteBrl: new Money(7_000_000m, Moeda.Brl));

        limite.Historico.Should().HaveCount(2);
        var ultimo = limite.Historico.Last();
        ultimo.ValorAnteriorBrl!.Value.Valor.Should().Be(5_000_000m);
        ultimo.ValorNovoBrl.Valor.Should().Be(7_000_000m);
        ultimo.RegistradoEm.Should().Be(clockUpdate.GetCurrentInstant());
    }

    [Fact]
    public void Atualizar_para_valor_menor_tambem_registra_no_historico()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = CriarLimite(clockCriacao, valor: 10_000_000m);
        limite.Atualizar(clockUpdate, novoLimiteBrl: new Money(8_000_000m, Moeda.Brl));

        limite.Historico.Should().HaveCount(2);
        limite.Historico.Last().ValorNovoBrl.Valor.Should().Be(8_000_000m);
    }

    [Fact]
    public void Atualizar_sem_mudar_valor_nao_acrescenta_historico()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = CriarLimite(clockCriacao, valor: 10_000_000m);
        limite.Atualizar(clockUpdate, observacoes: "comentário sem alterar valor");

        limite.Historico.Should().ContainSingle();
    }

    [Fact]
    public void Atualizar_para_mesmo_valor_explicitamente_nao_acrescenta_historico()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = CriarLimite(clockCriacao, valor: 10_000_000m);
        limite.Atualizar(clockUpdate, novoLimiteBrl: new Money(10_000_000m, Moeda.Brl));

        limite.Historico.Should().ContainSingle();
    }

    [Fact]
    public void Multiplas_atualizacoes_de_valor_geram_multiplas_entradas_em_ordem()
    {
        var c1 = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var c2 = PropostaFactory.CriarClockFixo(2026, 3, 1);
        var c3 = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = CriarLimite(c1, valor: 5_000_000m);
        limite.Atualizar(c2, novoLimiteBrl: new Money(7_000_000m, Moeda.Brl));
        limite.Atualizar(c3, novoLimiteBrl: new Money(10_000_000m, Moeda.Brl));

        limite.Historico.Should().HaveCount(3);
        limite.Historico.Select(h => h.ValorNovoBrl.Valor)
            .Should().Equal(5_000_000m, 7_000_000m, 10_000_000m);
    }

    [Fact]
    public void Entrada_de_historico_tem_FK_para_limite()
    {
        var clock = PropostaFactory.CriarClockFixo(2026, 1, 1);

        var limite = CriarLimite(clock);

        limite.Historico.Single().LimiteBancoId.Should().Be(limite.Id);
    }
}
