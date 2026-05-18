using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Contratos;
using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

/// <summary>
/// Testes de unidade para <see cref="CapitalDeGiroDetail"/> — Onda 3b.
/// SPEC §3.3: sem TipoProduto, sem TemFgi; apenas NumeroOperacao.
/// </summary>
public sealed class CapitalDeGiroDetailTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 10, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    /// <summary>
    /// Criar com inputs válidos deve retornar entidade preenchida corretamente.
    /// </summary>
    [Fact]
    public void Criar_com_inputs_validos_retorna_entidade_preenchida()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        Guid contratoId = Guid.NewGuid();
        const string NumeroOp = "OP-7788-0001";

        CapitalDeGiroDetail detail = CapitalDeGiroDetail.Criar(contratoId, NumeroOp, clock);

        detail.ContratoId.Should().Be(contratoId);
        detail.NumeroOperacao.Should().Be(NumeroOp);
        detail.CreatedAt.Should().Be(AgentInstant);
        detail.UpdatedAt.Should().Be(AgentInstant);
        detail.Id.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// NumeroOperacao null é aceito — campo opcional (SPEC EC-10).
    /// </summary>
    [Fact]
    public void Criar_com_numeroOperacao_null_e_aceito()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        CapitalDeGiroDetail detail = CapitalDeGiroDetail.Criar(Guid.NewGuid(), null, clock);

        detail.NumeroOperacao.Should().BeNull();
    }

    /// <summary>
    /// ContratoId vazio deve lançar ArgumentException — invariante de integridade.
    /// </summary>
    [Fact]
    public void Criar_com_contratoId_vazio_lanca_excecao()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        var act = () => CapitalDeGiroDetail.Criar(Guid.Empty, null, clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*contratoId*");
    }

    /// <summary>
    /// CapitalDeGiroDetail NÃO deve expor TipoProduto — campo removido na Onda 3b.
    /// </summary>
    [Fact]
    public void Nao_possui_propriedade_TipoProduto()
    {
        var tipo = typeof(CapitalDeGiroDetail);
        tipo.GetProperty("TipoProduto").Should().BeNull(
            because: "TipoProduto foi removido na Onda 3b — SPEC §3.3 e §1.1");
    }

    /// <summary>
    /// CapitalDeGiroDetail NÃO deve expor TemFgi — campo removido na Onda 3b.
    /// </summary>
    [Fact]
    public void Nao_possui_propriedade_TemFgi()
    {
        var tipo = typeof(CapitalDeGiroDetail);
        tipo.GetProperty("TemFgi").Should().BeNull(
            because: "TemFgi foi removido na Onda 3b — SPEC §3.4 e §1.1");
    }
}
