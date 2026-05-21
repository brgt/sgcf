using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.OrcamentosEncargo;
using Xunit;

namespace Sgcf.Domain.Tests.OrcamentosEncargo;

[Trait("Category", "Domain")]
public sealed class OrcamentoEncargoTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 21, 10, 0);

    // ── Happy path: Criar ──────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Guid contratoId = Guid.NewGuid();

        // Act
        OrcamentoEncargo orcamento = OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 1_500.25m,
            bancoId: bancoId,
            contratoId: contratoId,
            observacao: "Orçamento trimestral",
            agora: AgoraFixo);

        // Assert
        orcamento.Ano.Should().Be(2026);
        orcamento.Mes.Should().Be(5);
        orcamento.TipoEncargo.Should().Be("JUROS");
        orcamento.ValorOrcadoBrl.Valor.Should().Be(1_500.25m);
        orcamento.ValorOrcadoBrl.Moeda.Should().Be(Moeda.Brl);
        orcamento.BancoId.Should().Be(bancoId);
        orcamento.ContratoId.Should().Be(contratoId);
        orcamento.Observacao.Should().Be("Orçamento trimestral");
        orcamento.CriadoEm.Should().Be(AgoraFixo);
        orcamento.AtualizadoEm.Should().Be(AgoraFixo);
        orcamento.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Criar_SemBancoEContrato_PermiteNulos()
    {
        // Act
        OrcamentoEncargo orcamento = OrcamentoEncargo.Criar(
            ano: 2025,
            mes: 1,
            tipoEncargo: "IOF",
            valorOrcadoBrl: 0m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        orcamento.BancoId.Should().BeNull();
        orcamento.ContratoId.Should().BeNull();
        orcamento.Observacao.Should().BeNull();
        orcamento.ValorOrcadoBrlDecimal.Should().Be(0m);
    }

    // ── Guard: tipo vazio ──────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_TipoEncargoVazio_LancaArgumentException(string tipoInvalido)
    {
        // Act
        Action act = () => OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: tipoInvalido,
            valorOrcadoBrl: 100m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoEncargo");
    }

    // ── Guard: valor negativo ──────────────────────────────────────────────

    [Fact]
    public void Criar_ValorNegativo_LancaArgumentException()
    {
        // Act
        Action act = () => OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: -0.01m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("valor");
    }

    // ── Guard: ano fora do intervalo ───────────────────────────────────────

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Criar_AnoForaDoIntervalo_LancaArgumentOutOfRangeException(int anoInvalido)
    {
        // Act
        Action act = () => OrcamentoEncargo.Criar(
            ano: anoInvalido,
            mes: 6,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 100m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ano");
    }

    // ── Guard: mês fora do intervalo ───────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Criar_MesForaDoIntervalo_LancaArgumentOutOfRangeException(int mesInvalido)
    {
        // Act
        Action act = () => OrcamentoEncargo.Criar(
            ano: 2026,
            mes: mesInvalido,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 100m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("mes");
    }

    // ── Mutator: Atualizar ─────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ComValoresValidos_AtualizaPropriedades()
    {
        // Arrange
        Instant depois = Instant.FromUtc(2026, 5, 21, 11, 0);

        OrcamentoEncargo orcamento = OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 1_000m,
            bancoId: null,
            contratoId: null,
            observacao: "Observação original",
            agora: AgoraFixo);

        // Act
        orcamento.Atualizar(2_500.75m, "Observação revisada", depois);

        // Assert
        orcamento.ValorOrcadoBrlDecimal.Should().Be(2_500.75m);
        orcamento.Observacao.Should().Be("Observação revisada");
        orcamento.AtualizadoEm.Should().Be(depois);
        // CriadoEm não deve ser alterado pelo Atualizar.
        orcamento.CriadoEm.Should().Be(AgoraFixo);
    }

    [Fact]
    public void Atualizar_ValorNegativo_LancaArgumentException()
    {
        // Arrange
        OrcamentoEncargo orcamento = OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 1_000m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Act
        Action act = () => orcamento.Atualizar(-1m, null, AgoraFixo);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("valor");
    }

    // ── Arredondamento a 4 casas decimais ─────────────────────────────────

    [Fact]
    public void Criar_ValorArredondado4CasasDecimais()
    {
        // Arrange — 100.12345 arredondado para 100.1235 (HalfUp)
        OrcamentoEncargo orcamento = OrcamentoEncargo.Criar(
            ano: 2026,
            mes: 5,
            tipoEncargo: "JUROS",
            valorOrcadoBrl: 100.12345m,
            bancoId: null,
            contratoId: null,
            observacao: null,
            agora: AgoraFixo);

        // Assert
        orcamento.ValorOrcadoBrlDecimal.Should().Be(100.1235m);
    }
}
