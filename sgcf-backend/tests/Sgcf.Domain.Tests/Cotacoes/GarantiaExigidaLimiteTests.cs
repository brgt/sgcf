using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class GarantiaExigidaLimiteTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly Guid LimiteId = Guid.NewGuid();

    // ─── Factory Criar — sucessos ────────────────────────────────────────────

    [Fact]
    public void Criar_com_percentual_valido_e_sem_valor_fixo_deve_ter_sucesso()
    {
        var garantia = GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: 20m,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        garantia.LimiteBancoId.Should().Be(LimiteId);
        garantia.Tipo.Should().Be(TipoGarantia.CdbCativo);
        garantia.PercentualSobreLimite.Should().Be(20m);
        garantia.ValorFixoBrl.Should().BeNull();
        garantia.Obrigatoria.Should().BeTrue();
        garantia.CreatedAt.Should().Be(Clock.GetCurrentInstant());
        garantia.UpdatedAt.Should().Be(Clock.GetCurrentInstant());
    }

    [Fact]
    public void Criar_com_valor_fixo_valido_e_sem_percentual_deve_ter_sucesso()
    {
        var valor = new Money(200_000m, Moeda.Brl);

        var garantia = GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: null,
            valorFixoBrl: valor,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        garantia.PercentualSobreLimite.Should().BeNull();
        garantia.ValorFixoBrl.Should().Be(valor);
    }

    [Fact]
    public void Criar_Aval_sem_percentual_e_sem_valor_fixo_deve_ter_sucesso()
    {
        // Para Aval, ambos campos podem ser nulos (representa 100% do empréstimo implicitamente).
        var garantia = GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.Aval,
            percentualSobreLimite: null,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: "Aval dos sócios",
            clock: Clock);

        garantia.Tipo.Should().Be(TipoGarantia.Aval);
        garantia.PercentualSobreLimite.Should().BeNull();
        garantia.ValorFixoBrl.Should().BeNull();
        garantia.Observacoes.Should().Be("Aval dos sócios");
    }

    [Fact]
    public void Criar_garantia_opcional_deve_persistir_flag_falsa()
    {
        var garantia = GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.Fgi,
            percentualSobreLimite: 10m,
            valorFixoBrl: null,
            obrigatoria: false,
            observacoes: null,
            clock: Clock);

        garantia.Obrigatoria.Should().BeFalse();
    }

    // ─── Factory Criar — rejeições ───────────────────────────────────────────

    [Fact]
    public void Criar_nao_Aval_com_ambos_campos_nulos_deve_lancar_excecao()
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: null,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*percentual*valor*");
    }

    [Fact]
    public void Criar_com_ambos_campos_preenchidos_deve_lancar_excecao()
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: 20m,
            valorFixoBrl: new Money(200_000m, Moeda.Brl),
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*mutuamente exclusivos*");
    }

    [Fact]
    public void Criar_Aval_com_ambos_campos_preenchidos_deve_lancar_excecao()
    {
        // A relaxação para Aval permite ambos nulos, mas não ambos preenchidos.
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.Aval,
            percentualSobreLimite: 100m,
            valorFixoBrl: new Money(1_000_000m, Moeda.Brl),
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*mutuamente exclusivos*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    [InlineData(150)]
    public void Criar_com_percentual_fora_do_intervalo_deve_lancar_excecao(decimal percentual)
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: percentual,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*percentual*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Criar_com_valor_fixo_nao_positivo_deve_lancar_excecao(decimal valor)
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: null,
            valorFixoBrl: new Money(valor, Moeda.Brl),
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");
    }

    [Fact]
    public void Criar_com_valor_fixo_em_moeda_diferente_de_BRL_deve_lancar_excecao()
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: null,
            valorFixoBrl: new Money(200_000m, Moeda.Usd),
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    [Fact]
    public void Criar_com_limiteBancoId_vazio_deve_lancar_excecao()
    {
        var act = () => GarantiaExigidaLimite.Criar(
            limiteBancoId: Guid.Empty,
            tipo: TipoGarantia.CdbCativo,
            percentualSobreLimite: 20m,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*LimiteBancoId*");
    }

    // ─── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_pode_trocar_percentual_por_valor_fixo()
    {
        var garantia = GarantiaExigidaLimite.Criar(
            LimiteId, TipoGarantia.CdbCativo,
            percentualSobreLimite: 20m, valorFixoBrl: null,
            obrigatoria: true, observacoes: null, clock: Clock);

        var clockDepois = PropostaFactory.CriarClockFixo(2026, 6, 1);
        garantia.Atualizar(
            percentualSobreLimite: null,
            valorFixoBrl: new Money(150_000m, Moeda.Brl),
            obrigatoria: true,
            observacoes: "Renegociado",
            clock: clockDepois);

        garantia.PercentualSobreLimite.Should().BeNull();
        garantia.ValorFixoBrl!.Value.Valor.Should().Be(150_000m);
        garantia.Observacoes.Should().Be("Renegociado");
        garantia.UpdatedAt.Should().Be(clockDepois.GetCurrentInstant());
    }

    [Fact]
    public void Atualizar_aplica_mesmas_validacoes_da_criacao()
    {
        var garantia = GarantiaExigidaLimite.Criar(
            LimiteId, TipoGarantia.CdbCativo,
            percentualSobreLimite: 20m, valorFixoBrl: null,
            obrigatoria: true, observacoes: null, clock: Clock);

        var act = () => garantia.Atualizar(
            percentualSobreLimite: 200m,
            valorFixoBrl: null,
            obrigatoria: true,
            observacoes: null,
            clock: Clock);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
