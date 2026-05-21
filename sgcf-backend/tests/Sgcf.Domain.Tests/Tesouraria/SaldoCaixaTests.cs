using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;
using Xunit;

namespace Sgcf.Domain.Tests.Tesouraria;

[Trait("Category", "Domain")]
public sealed class SaldoCaixaTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static readonly Instant InstanteBase = Instant.FromUtc(2026, 5, 21, 10, 0);
    private static readonly LocalDate DataBase = new(2026, 5, 21);
    private static readonly Guid ContaIdBase = Guid.NewGuid();
    private static readonly Money ValorBase = new(1000m, Moeda.Brl);

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        IClock clock = CriarClock(InstanteBase);

        SaldoCaixa saldo = SaldoCaixa.Criar(ContaIdBase, DataBase, ValorBase, "operador@empresa.com", clock);

        saldo.Id.Should().NotBeEmpty();
        saldo.ContaId.Should().Be(ContaIdBase);
        saldo.DataReferencia.Should().Be(DataBase);
        saldo.Valor.Should().Be(ValorBase);
        saldo.RegistradoPor.Should().Be("operador@empresa.com");
        saldo.RegistradoEm.Should().Be(InstanteBase);
    }

    [Fact]
    public void Atualizar_RetornaValorAnterior()
    {
        IClock clockCriacao = CriarClock(InstanteBase);
        IClock clockUpdate = CriarClock(InstanteBase.Plus(Duration.FromHours(1)));
        SaldoCaixa saldo = SaldoCaixa.Criar(ContaIdBase, DataBase, ValorBase, "operador", clockCriacao);

        Money novoValor = new(2000m, Moeda.Brl);

        Money valorRetornado = saldo.Atualizar(novoValor, "supervisor", clockUpdate);

        valorRetornado.Should().Be(ValorBase);
        saldo.Valor.Should().Be(novoValor);
    }

    [Fact]
    public void Atualizar_DuasVezes_MantemUltimoValor()
    {
        IClock clock1 = CriarClock(InstanteBase);
        IClock clock2 = CriarClock(InstanteBase.Plus(Duration.FromHours(1)));
        IClock clock3 = CriarClock(InstanteBase.Plus(Duration.FromHours(2)));

        SaldoCaixa saldo = SaldoCaixa.Criar(ContaIdBase, DataBase, ValorBase, "operador", clock1);

        Money segundoValor = new(2000m, Moeda.Brl);
        Money terceiroValor = new(3000m, Moeda.Brl);

        saldo.Atualizar(segundoValor, "operador", clock2);
        Money valorAntesTerceiro = saldo.Atualizar(terceiroValor, "supervisor", clock3);

        valorAntesTerceiro.Should().Be(segundoValor);
        saldo.Valor.Should().Be(terceiroValor);
    }
}
