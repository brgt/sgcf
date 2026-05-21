using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Covenants;
using Xunit;

namespace Sgcf.Domain.Tests.Covenants;

/// <summary>
/// Testes unitários para Covenant. GAP-CKP-13.
/// </summary>
public sealed class CovenantTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2025, 6, 1, 0, 0, 0);
    private static readonly Guid ContratoIdFixo = Guid.NewGuid();
    private static readonly LocalDate DataFixa = new(2025, 9, 30);

    [Fact]
    public void Criar_ComParametrosValidos_RetornaCovenantPendente()
    {
        Covenant c = Covenant.Criar(
            ContratoIdFixo, "Dívida/EBITDA ≤ 3x",
            TipoCovenant.Financeiro, 3, DataFixa, 3.0m, AgoraFixo);

        c.ContratoId.Should().Be(ContratoIdFixo);
        c.Descricao.Should().Be("Dívida/EBITDA ≤ 3x");
        c.Tipo.Should().Be(TipoCovenant.Financeiro);
        c.Status.Should().Be(StatusCovenant.Pendente);
        c.PeriodicidadeVerificacaoMeses.Should().Be(3);
        c.ProximaVerificacaoEm.Should().Be(DataFixa);
        c.LimiteNumerico.Should().Be(3.0m);
        c.CriadoEm.Should().Be(AgoraFixo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComDescricaoVazia_LancaArgumentException(string descricao)
    {
        Action act = () => Covenant.Criar(
            ContratoIdFixo, descricao, TipoCovenant.Financeiro, 3, null, null, AgoraFixo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_ComContratoIdVazio_LancaArgumentException()
    {
        Action act = () => Covenant.Criar(
            Guid.Empty, "Covenant válido", TipoCovenant.Financeiro, 3, null, null, AgoraFixo);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ComPeriodicidadeInvalida_LancaArgumentOutOfRangeException(int meses)
    {
        Action act = () => Covenant.Criar(
            ContratoIdFixo, "Desc", TipoCovenant.Financeiro, meses, null, null, AgoraFixo);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RegistrarVerificacao_Violado_AlteraStatus()
    {
        Covenant c = Covenant.Criar(
            ContratoIdFixo, "Cobertura ≥ 1.2",
            TipoCovenant.Financeiro, 3, DataFixa, 1.2m, AgoraFixo);

        Instant verificacaoEm = AgoraFixo.Plus(Duration.FromDays(10));
        c.RegistrarVerificacao(
            StatusCovenant.Violado,
            DataFixa,
            DataFixa.PlusMonths(3),
            0.9m,
            "Índice apurado abaixo do mínimo",
            verificacaoEm);

        c.Status.Should().Be(StatusCovenant.Violado);
        c.UltimaVerificacaoEm.Should().Be(DataFixa);
        c.ValorApurado.Should().Be(0.9m);
        c.ObservacaoVerificacao.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Atualizar_ComDescricaoVazia_LancaArgumentException()
    {
        Covenant c = Covenant.Criar(
            ContratoIdFixo, "Original", TipoCovenant.NaoFinanceiro, 12, null, null, AgoraFixo);

        Action act = () => c.Atualizar("", 12, null, null, AgoraFixo);

        act.Should().Throw<ArgumentException>();
    }
}
