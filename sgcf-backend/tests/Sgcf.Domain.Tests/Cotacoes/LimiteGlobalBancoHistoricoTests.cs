using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class LimiteGlobalBancoHistoricoTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 1, 1, 12, 0, 0);
    private static readonly Guid LimiteGlobalBancoId = Guid.NewGuid();

    // ─── Validações da factory Criar ─────────────────────────────────────────

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComLimiteIdVazio_LancaArgumentException()
    {
        var act = () => InvocarCriarInterno(
            limiteGlobalBancoId: Guid.Empty,
            valorNovoBrl: new Money(1_000_000m, Moeda.Brl));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("limiteGlobalBancoId");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComMoedaDiferenteDeBrl_LancaArgumentException()
    {
        var act = () => InvocarCriarInterno(
            limiteGlobalBancoId: LimiteGlobalBancoId,
            valorNovoBrl: new Money(1_000_000m, Moeda.Usd));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComDadosValidos_RetornaHistoricoComValoresCorretos()
    {
        var valorAnterior = new Money(500_000m, Moeda.Brl);
        var valorNovo = new Money(1_000_000m, Moeda.Brl);

        var historico = InvocarCriarInterno(
            limiteGlobalBancoId: LimiteGlobalBancoId,
            valorAnteriorBrl: valorAnterior,
            valorNovoBrl: valorNovo,
            observacoes: "Aumento negociado");

        historico.LimiteGlobalBancoId.Should().Be(LimiteGlobalBancoId);
        historico.ValorAnteriorBrl!.Value.Valor.Should().Be(500_000m);
        historico.ValorNovoBrl.Valor.Should().Be(1_000_000m);
        historico.RegistradoEm.Should().Be(InstanteFixo);
        historico.Observacoes.Should().Be("Aumento negociado");
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_ComValorAnteriorNulo_ValorAnteriorBrlDeveSerNulo()
    {
        var historico = InvocarCriarInterno(
            limiteGlobalBancoId: LimiteGlobalBancoId,
            valorAnteriorBrl: null,
            valorNovoBrl: new Money(1_000_000m, Moeda.Brl));

        historico.ValorAnteriorBrl.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void Criar_EntradaInicial_FkApontaParaLimiteGlobalBanco()
    {
        var limite = CriarLimiteGlobal();

        limite.Historico.Single().LimiteGlobalBancoId.Should().Be(limite.Id);
    }

    // ─── Helper: invoca o método interno via o agregado pai ──────────────────

    /// <summary>
    /// Cria um <see cref="LimiteGlobalBancoHistorico"/> via reflexão
    /// para testar a factory interna diretamente.
    /// </summary>
    private static LimiteGlobalBancoHistorico InvocarCriarInterno(
        Guid limiteGlobalBancoId,
        Money? valorAnteriorBrl = null,
        Money? valorNovoBrl = null,
        string? observacoes = null)
    {
        var metodo = typeof(LimiteGlobalBancoHistorico)
            .GetMethod("Criar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        try
        {
            return (LimiteGlobalBancoHistorico)metodo.Invoke(null, [
                limiteGlobalBancoId,
                valorAnteriorBrl,
                valorNovoBrl ?? new Money(1_000_000m, Moeda.Brl),
                InstanteFixo,
                observacoes
            ])!;
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            throw; // unreachable — satisfies compiler
        }
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(decimal valor = 5_000_000m) =>
        LimiteGlobalBanco.Criar(
            bancoId: Guid.NewGuid(),
            valorLimiteBrl: new Money(valor, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: PropostaFactory.CriarClockFixo(2026, 1, 1));
}
