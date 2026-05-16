using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class LimiteBancoTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static LimiteBanco CriarLimite(
        decimal valorLimite = 10_000_000m,
        LocalDate? inicio = null,
        LocalDate? fim = null) =>
        LimiteBanco.Criar(
            bancoId: BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valorLimite, Moeda.Brl),
            dataVigenciaInicio: inicio ?? new LocalDate(2026, 1, 1),
            clock: Clock,
            dataVigenciaFim: fim);

    // ─── Factory Criar ───────────────────────────────────────────────────────

    [Fact]
    public void Criar_limite_valido_deve_ter_utilizado_zero()
    {
        var limite = CriarLimite();
        limite.ValorUtilizadoBrl.Valor.Should().Be(0m);
    }

    [Fact]
    public void Criar_limite_calcula_disponivel_igual_ao_limite()
    {
        var limite = CriarLimite(valorLimite: 5_000_000m);
        limite.ValorDisponivelBrl.Valor.Should().Be(5_000_000m);
    }

    [Fact]
    public void Criar_limite_com_valor_zero_deve_lancar_excecao()
    {
        var act = () => LimiteBanco.Criar(
            BancoId, ModalidadeContrato.Finimp,
            new Money(0m, Moeda.Brl),
            new LocalDate(2026, 1, 1), Clock);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");
    }

    [Fact]
    public void Criar_limite_com_DataVigenciaFim_anterior_a_inicio_deve_lancar_excecao()
    {
        var act = () => CriarLimite(
            inicio: new LocalDate(2026, 6, 1),
            fim: new LocalDate(2026, 5, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*DataVigenciaFim*posterior*");
    }

    [Fact]
    public void Criar_limite_com_moeda_nao_BRL_deve_lancar_excecao()
    {
        var act = () => LimiteBanco.Criar(
            BancoId, ModalidadeContrato.Finimp,
            new Money(1_000_000m, Moeda.Usd),
            new LocalDate(2026, 1, 1), Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ─── RegistrarUso ────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarUso_deve_incrementar_utilizado()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);
        var uso = new Money(2_000_000m, Moeda.Brl);

        limite.RegistrarUso(uso, Clock);

        limite.ValorUtilizadoBrl.Valor.Should().Be(2_000_000m);
        limite.ValorDisponivelBrl.Valor.Should().Be(8_000_000m);
    }

    [Fact]
    public void RegistrarUso_que_excede_limite_deve_lancar_excecao()
    {
        var limite = CriarLimite(valorLimite: 1_000_000m);

        var act = () => limite.RegistrarUso(new Money(1_000_001m, Moeda.Brl), Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*excederia*limite*");
    }

    [Fact]
    public void RegistrarUso_exatamente_no_limite_deve_ser_permitido()
    {
        var limite = CriarLimite(valorLimite: 5_000_000m);

        limite.RegistrarUso(new Money(5_000_000m, Moeda.Brl), Clock);

        limite.ValorUtilizadoBrl.Valor.Should().Be(5_000_000m);
        limite.ValorDisponivelBrl.Valor.Should().Be(0m);
    }

    [Fact]
    public void RegistrarUso_com_valor_zero_deve_lancar_excecao()
    {
        var limite = CriarLimite();
        var act = () => limite.RegistrarUso(new Money(0m, Moeda.Brl), Clock);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");
    }

    // ─── LiberarUso ──────────────────────────────────────────────────────────

    [Fact]
    public void LiberarUso_deve_decrementar_utilizado()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);
        limite.RegistrarUso(new Money(3_000_000m, Moeda.Brl), Clock);

        limite.LiberarUso(new Money(1_000_000m, Moeda.Brl), Clock);

        limite.ValorUtilizadoBrl.Valor.Should().Be(2_000_000m);
    }

    [Fact]
    public void LiberarUso_que_resulta_negativo_deve_lancar_excecao()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);
        limite.RegistrarUso(new Money(1_000_000m, Moeda.Brl), Clock);

        var act = () => limite.LiberarUso(new Money(2_000_000m, Moeda.Brl), Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negativo*");
    }

    [Fact]
    public void LiberarUso_com_valor_zero_deve_lancar_excecao()
    {
        var limite = CriarLimite();
        var act = () => limite.LiberarUso(new Money(0m, Moeda.Brl), Clock);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");
    }

    // ─── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_limite_para_novo_valor_maior_deve_funcionar()
    {
        var limite = CriarLimite(valorLimite: 5_000_000m);
        limite.Atualizar(Clock, novoLimiteBrl: new Money(10_000_000m, Moeda.Brl));
        limite.ValorLimiteBrl.Valor.Should().Be(10_000_000m);
    }

    [Fact]
    public void Atualizar_limite_para_valor_menor_que_utilizado_deve_lancar_excecao()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);
        limite.RegistrarUso(new Money(8_000_000m, Moeda.Brl), Clock);

        var act = () => limite.Atualizar(Clock, novoLimiteBrl: new Money(5_000_000m, Moeda.Brl));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*menor*utilizado*");
    }

    [Fact]
    public void ValorDisponivelBrl_nao_pode_ser_negativo()
    {
        var limite = CriarLimite(valorLimite: 5_000_000m);
        limite.ValorDisponivelBrl.Valor.Should().BeGreaterThanOrEqualTo(0m);
    }
}
