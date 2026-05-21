using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;
using Xunit;

namespace Sgcf.Domain.Tests.Tesouraria;

[Trait("Category", "Domain")]
public sealed class ContaBancariaTests
{
    private static readonly Guid BancoIdValido = Guid.NewGuid();

    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    // ── Criar — happy path ────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 21, 10, 0);
        IClock clock = CriarClock(agora);

        // Act
        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido,
            "Conta Corrente Principal",
            "1234",
            "12345-6",
            Moeda.Brl,
            clock);

        // Assert
        conta.Id.Should().NotBeEmpty();
        conta.BancoId.Should().Be(BancoIdValido);
        conta.Nome.Should().Be("Conta Corrente Principal");
        conta.Agencia.Should().Be("1234");
        conta.NumeroConta.Should().Be("12345-6");
        conta.Moeda.Should().Be(Moeda.Brl);
        conta.Ativa.Should().BeTrue();
        conta.CriadoEm.Should().Be(agora);
        conta.AtualizadoEm.Should().Be(agora);
        conta.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_TrimNosStrings_AplicadoCorretamente()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));

        // Act
        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "  Conta  ", "  0001  ", "  12345  ", Moeda.Usd, clock);

        // Assert
        conta.Nome.Should().Be("Conta");
        conta.Agencia.Should().Be("0001");
        conta.NumeroConta.Should().Be("12345");
    }

    // ── Criar — validações ───────────────────────────────────────────────────

    [Fact]
    public void Criar_BancoIdVazio_LancaArgumentException()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));

        Action act = () => ContaBancaria.Criar(Guid.Empty, "Nome", "0001", "12345", Moeda.Brl, clock);

        act.Should().Throw<ArgumentException>().WithParameterName("bancoId");
    }

    [Fact]
    public void Criar_NomeVazio_LancaArgumentException()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));

        Action act = () => ContaBancaria.Criar(BancoIdValido, "   ", "0001", "12345", Moeda.Brl, clock);

        act.Should().Throw<ArgumentException>().WithParameterName("nome");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaArgumentException()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));
        string nomeLongo = new string('X', 201);

        Action act = () => ContaBancaria.Criar(BancoIdValido, nomeLongo, "0001", "12345", Moeda.Brl, clock);

        act.Should().Throw<ArgumentException>().WithParameterName("nome");
    }

    [Fact]
    public void Criar_AgenciaVazia_LancaArgumentException()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));

        Action act = () => ContaBancaria.Criar(BancoIdValido, "Nome", "   ", "12345", Moeda.Brl, clock);

        act.Should().Throw<ArgumentException>().WithParameterName("agencia");
    }

    [Fact]
    public void Criar_NumeroContaVazio_LancaArgumentException()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));

        Action act = () => ContaBancaria.Criar(BancoIdValido, "Nome", "0001", "   ", Moeda.Brl, clock);

        act.Should().Throw<ArgumentException>().WithParameterName("numeroConta");
    }

    // ── Atualizar ────────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ComDadosValidos_MudaPropriedadesEAtualizadoEm()
    {
        // Arrange
        Instant criadoEm = Instant.FromUtc(2026, 5, 21, 10, 0);
        Instant atualizadoEm = Instant.FromUtc(2026, 5, 21, 12, 0);
        IClock clockCriacao = CriarClock(criadoEm);
        IClock clockAtualizacao = CriarClock(atualizadoEm);

        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Nome Antigo", "0001", "11111", Moeda.Brl, clockCriacao);

        // Act
        conta.Atualizar("Nome Novo", "0002", "22222", Moeda.Usd, clockAtualizacao);

        // Assert
        conta.Nome.Should().Be("Nome Novo");
        conta.Agencia.Should().Be("0002");
        conta.NumeroConta.Should().Be("22222");
        conta.Moeda.Should().Be(Moeda.Usd);
        conta.AtualizadoEm.Should().Be(atualizadoEm);
        conta.CriadoEm.Should().Be(criadoEm); // não muda
    }

    // ── Deletar (soft delete) ────────────────────────────────────────────────

    [Fact]
    public void Deletar_SetaDeletedAt()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 21, 10, 0);
        Instant deletadoEm = Instant.FromUtc(2026, 5, 21, 15, 0);
        IClock clockCriacao = CriarClock(agora);
        IClock clockDeleção = CriarClock(deletadoEm);

        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clockCriacao);

        // Act
        conta.Deletar(clockDeleção);

        // Assert
        conta.DeletedAt.Should().Be(deletadoEm);
        conta.AtualizadoEm.Should().Be(deletadoEm);
    }

    [Fact]
    public void Deletar_DuasVezes_EhIdempotente()
    {
        // Arrange
        Instant primeiraDelecao = Instant.FromUtc(2026, 5, 21, 15, 0);
        Instant segundaDelecao = Instant.FromUtc(2026, 5, 21, 16, 0);
        IClock clockCriacao = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));
        IClock clockPrimeiro = CriarClock(primeiraDelecao);
        IClock clockSegundo = CriarClock(segundaDelecao);

        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clockCriacao);

        // Act
        conta.Deletar(clockPrimeiro);
        conta.Deletar(clockSegundo); // segunda chamada deve ser no-op

        // Assert — timestamp da primeira deleção é preservado
        conta.DeletedAt.Should().Be(primeiraDelecao);
    }

    // ── Desativar / Reativar ─────────────────────────────────────────────────

    [Fact]
    public void Desativar_ContaAtiva_SetaAtivaFalso()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));
        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clock);

        // Act
        conta.Desativar(clock);

        // Assert
        conta.Ativa.Should().BeFalse();
    }

    [Fact]
    public void Reativar_ContaInativa_SetaAtivaVerdadeiro()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 21, 10, 0));
        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clock);
        conta.Desativar(clock);

        // Act
        conta.Reativar(clock);

        // Assert
        conta.Ativa.Should().BeTrue();
    }

    [Fact]
    public void Desativar_ContaJaInativa_EhIdempotente()
    {
        // Arrange
        Instant primeiraDesativacao = Instant.FromUtc(2026, 5, 21, 10, 0);
        Instant segundaDesativacao = Instant.FromUtc(2026, 5, 21, 11, 0);
        IClock clockCriacao = CriarClock(Instant.FromUtc(2026, 5, 21, 9, 0));
        IClock clockPrimeiro = CriarClock(primeiraDesativacao);
        IClock clockSegundo = CriarClock(segundaDesativacao);

        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clockCriacao);
        conta.Desativar(clockPrimeiro);

        // Act
        conta.Desativar(clockSegundo); // no-op

        // Assert — AtualizadoEm preservado da primeira desativação
        conta.AtualizadoEm.Should().Be(primeiraDesativacao);
    }

    [Fact]
    public void Reativar_ContaJaAtiva_EhIdempotente()
    {
        // Arrange
        Instant criadoEm = Instant.FromUtc(2026, 5, 21, 10, 0);
        Instant tentativaReativacao = Instant.FromUtc(2026, 5, 21, 11, 0);
        IClock clockCriacao = CriarClock(criadoEm);
        IClock clockReativacao = CriarClock(tentativaReativacao);

        ContaBancaria conta = ContaBancaria.Criar(
            BancoIdValido, "Conta", "0001", "12345", Moeda.Brl, clockCriacao);

        // Act — conta já está ativa; reativar deve ser no-op
        conta.Reativar(clockReativacao);

        // Assert — AtualizadoEm não mudou
        conta.AtualizadoEm.Should().Be(criadoEm);
    }
}
