using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class LimiteGlobalBancoTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly Guid BancoId = Guid.NewGuid();

    private static LimiteGlobalBanco CriarLimite(
        decimal valorLimite = 10_000_000m,
        LocalDate? inicio = null,
        LocalDate? fim = null) =>
        LimiteGlobalBanco.Criar(
            bancoId: BancoId,
            valorLimiteBrl: new Money(valorLimite, Moeda.Brl),
            dataVigenciaInicio: inicio ?? new LocalDate(2026, 1, 1),
            clock: Clock,
            dataVigenciaFim: fim);

    // ─── LG-01: moeda deve ser BRL ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComMoedaDiferenteDeBrl_LancaArgumentException()
    {
        var act = () => LimiteGlobalBanco.Criar(
            bancoId: BancoId,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Usd),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ─── LG-02: valor deve ser positivo ──────────────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComValorZeroOuNegativo_LancaArgumentOutOfRangeException()
    {
        var actZero = () => LimiteGlobalBanco.Criar(
            BancoId, new Money(0m, Moeda.Brl), new LocalDate(2026, 1, 1), Clock);

        var actNegativo = () => LimiteGlobalBanco.Criar(
            BancoId, new Money(-1m, Moeda.Brl), new LocalDate(2026, 1, 1), Clock);

        actZero.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");

        actNegativo.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*positivo*");
    }

    // ─── LG-03: DataVigenciaFim deve ser posterior ao início ─────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComDataFimAnteriorOuIgualAInicio_LancaArgumentException()
    {
        var actIgual = () => CriarLimite(
            inicio: new LocalDate(2026, 6, 1),
            fim: new LocalDate(2026, 6, 1));

        var actAnterior = () => CriarLimite(
            inicio: new LocalDate(2026, 6, 1),
            fim: new LocalDate(2026, 5, 31));

        actIgual.Should().Throw<ArgumentException>()
            .WithMessage("*DataVigenciaFim*posterior*");

        actAnterior.Should().Throw<ArgumentException>()
            .WithMessage("*DataVigenciaFim*posterior*");
    }

    // ─── LG-06: redução abaixo do saldo devedor atual ────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Atualizar_ReduzindoAbaixoDoSaldoDevedor_LancaInvalidOperationException()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);
        var saldoAtual = new Money(8_000_000m, Moeda.Brl);
        var novoLimite = new Money(5_000_000m, Moeda.Brl); // < saldo

        var act = () => limite.Atualizar(
            clock: Clock,
            novoLimiteBrl: novoLimite,
            saldoDevedorAtual: saldoAtual);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*saldo devedor*");
    }

    // ─── LG-07: histórico na criação (ValorAnterior = null) ──────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComDadosValidos_ApendaEntradaHistoricoComValorAnteriorNulo()
    {
        var limite = CriarLimite(valorLimite: 5_000_000m);

        limite.Historico.Should().ContainSingle();
        var entrada = limite.Historico.Single();
        entrada.ValorAnteriorBrl.Should().BeNull();
        entrada.ValorNovoBrl.Valor.Should().Be(5_000_000m);
    }

    // ─── LG-07: histórico appended ao atualizar com novo valor ───────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Atualizar_ComNovoValor_ApendaNovaEntradaHistorico()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 1);

        var limite = LimiteGlobalBanco.Criar(
            BancoId, new Money(5_000_000m, Moeda.Brl), new LocalDate(2026, 1, 1), clockCriacao);

        limite.Atualizar(clockUpdate, novoLimiteBrl: new Money(8_000_000m, Moeda.Brl));

        limite.Historico.Should().HaveCount(2);
        var ultima = limite.Historico.Last();
        ultima.ValorAnteriorBrl!.Value.Valor.Should().Be(5_000_000m);
        ultima.ValorNovoBrl.Valor.Should().Be(8_000_000m);
        ultima.RegistradoEm.Should().Be(clockUpdate.GetCurrentInstant());
    }

    // ─── LG-07: sem duplicata quando valor não muda ──────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Atualizar_ComMesmoValor_NaoApendaEntradaDuplicada()
    {
        var limite = CriarLimite(valorLimite: 10_000_000m);

        limite.Atualizar(Clock, novoLimiteBrl: new Money(10_000_000m, Moeda.Brl));

        limite.Historico.Should().ContainSingle("valor idêntico não deve gerar entrada de histórico");
    }

    // ─── LG-08: encerrar vigência já encerrada ───────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void EncerrarVigencia_QuandoJaEncerrada_LancaInvalidOperationException()
    {
        var limite = CriarLimite(
            inicio: new LocalDate(2026, 1, 1),
            fim: new LocalDate(2026, 12, 31));

        var act = () => limite.EncerrarVigencia(new LocalDate(2026, 6, 30), Clock);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Vigência já encerrada*");
    }

    // ─── LG-08: data de fim anterior ao início ───────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void EncerrarVigencia_ComDataAnteriorAoInicio_LancaArgumentException()
    {
        var limite = CriarLimite(inicio: new LocalDate(2026, 6, 1));

        var act = () => limite.EncerrarVigencia(new LocalDate(2026, 5, 31), Clock);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("dataFim");
    }

    // ─── Happy path: criação retorna agregado com histórico inicial ───────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComDadosValidos_RetornaAgregadoComHistoricoInicial()
    {
        var clock = PropostaFactory.CriarClockFixo(2026, 3, 15);
        var inicio = new LocalDate(2026, 4, 1);

        var limite = LimiteGlobalBanco.Criar(
            bancoId: BancoId,
            valorLimiteBrl: new Money(2_000_000m, Moeda.Brl),
            dataVigenciaInicio: inicio,
            clock: clock,
            observacoes: "Linha BB FINIMP");

        limite.BancoId.Should().Be(BancoId);
        limite.ValorLimiteBrl.Valor.Should().Be(2_000_000m);
        limite.ValorLimiteBrl.Moeda.Should().Be(Moeda.Brl);
        limite.DataVigenciaInicio.Should().Be(inicio);
        limite.DataVigenciaFim.Should().BeNull();
        limite.Observacoes.Should().Be("Linha BB FINIMP");
        limite.CreatedAt.Should().Be(clock.GetCurrentInstant());
        limite.UpdatedAt.Should().Be(clock.GetCurrentInstant());
        limite.Historico.Should().ContainSingle();
    }

    // ─── Boundary: EncerrarVigencia com data igual ao início é permitido ─────

    [Fact]
    [Trait("Category", "Domain")]
    public void EncerrarVigencia_ComDataIgualAoInicio_Aceita()
    {
        var inicio = new LocalDate(2026, 6, 1);
        var limite = CriarLimite(inicio: inicio);

        var act = () => limite.EncerrarVigencia(inicio, Clock);

        act.Should().NotThrow("data igual ao início deve ser aceita — >= é permitido");
        limite.DataVigenciaFim.Should().Be(inicio);
    }

    // ─── Atualizar sem novo limite não lança exceção ──────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Atualizar_SemNovoLimite_NaoLancaExcecao()
    {
        var limite = CriarLimite();

        var act = () => limite.Atualizar(Clock, observacoes: "Apenas comentário");

        act.Should().NotThrow();
        limite.Historico.Should().ContainSingle("sem alteração de valor, sem novo histórico");
    }

    // ─── Atualizar atualiza UpdatedAt ─────────────────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Atualizar_AtualizaUpdatedAt()
    {
        var clockCriacao = PropostaFactory.CriarClockFixo(2026, 1, 1);
        var clockUpdate = PropostaFactory.CriarClockFixo(2026, 6, 15);

        var limite = LimiteGlobalBanco.Criar(
            BancoId, new Money(5_000_000m, Moeda.Brl), new LocalDate(2026, 1, 1), clockCriacao);

        limite.Atualizar(clockUpdate, observacoes: "atualização");

        limite.UpdatedAt.Should().Be(clockUpdate.GetCurrentInstant());
    }
}
