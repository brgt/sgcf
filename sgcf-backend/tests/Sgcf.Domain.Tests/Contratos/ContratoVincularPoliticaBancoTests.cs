using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

/// <summary>
/// Testes de domínio para <see cref="Contrato.VincularPoliticaBanco"/>.
/// Cobre a invariante SC-05 (SPEC §4.3): os três campos de política do banco
/// são imutáveis após preenchimento e idempotentes para mesmos valores.
/// </summary>
[Trait("Category", "Domain")]
public sealed class ContratoVincularPoliticaBancoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Contrato CriarContratoValido()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 25, 10, 0));
        return Contrato.Criar(
            numeroExterno: "FIN-2026-SC05",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(2_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    // ── SC-05 — primeira chamada preenche os três campos ─────────────────────

    [Fact]
    public void VincularPoliticaBanco_PrimeiraChamada_PreencheTresCampos()
    {
        // Arrange
        Contrato contrato = CriarContratoValido();
        Guid limiteBancoId = Guid.NewGuid();
        Guid limiteGlobalBancoId = Guid.NewGuid();
        Guid garantiasRevisaoId = Guid.NewGuid();

        // Act
        contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalBancoId, garantiasRevisaoId);

        // Assert
        contrato.LimiteBancoId.Should().Be(limiteBancoId);
        contrato.LimiteGlobalBancoId.Should().Be(limiteGlobalBancoId);
        contrato.GarantiasExigidasRevisaoId.Should().Be(garantiasRevisaoId);
    }

    // ── SC-05 — idempotência: mesmos valores não lançam exceção ─────────────

    [Fact]
    public void VincularPoliticaBanco_RechamadaComMesmosValores_NaoLanca()
    {
        // Arrange
        Contrato contrato = CriarContratoValido();
        Guid limiteBancoId = Guid.NewGuid();
        Guid limiteGlobalBancoId = Guid.NewGuid();
        Guid garantiasRevisaoId = Guid.NewGuid();

        contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalBancoId, garantiasRevisaoId);

        // Act — rechamada com exatamente os mesmos valores deve ser no-op silencioso.
        Action act = () => contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalBancoId, garantiasRevisaoId);

        // Assert
        act.Should().NotThrow();
        contrato.LimiteBancoId.Should().Be(limiteBancoId);
        contrato.LimiteGlobalBancoId.Should().Be(limiteGlobalBancoId);
        contrato.GarantiasExigidasRevisaoId.Should().Be(garantiasRevisaoId);
    }

    // ── SC-05 — imutabilidade: LimiteBancoId não pode ser alterado ──────────

    [Fact]
    public void VincularPoliticaBanco_TentaAlterarLimiteBancoId_LancaInvalidOperationException()
    {
        // Arrange
        Contrato contrato = CriarContratoValido();
        Guid idOriginal = Guid.NewGuid();
        Guid limiteGlobalId = Guid.NewGuid();
        Guid revisaoId = Guid.NewGuid();

        contrato.VincularPoliticaBanco(idOriginal, limiteGlobalId, revisaoId);

        Guid idDiferente = Guid.NewGuid();

        // Act — tentar substituir LimiteBancoId por valor diferente deve lançar.
        Action act = () => contrato.VincularPoliticaBanco(idDiferente, limiteGlobalId, revisaoId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LimiteBancoId*");
    }

    // ── SC-05 — imutabilidade: LimiteGlobalBancoId não pode ser alterado ────

    [Fact]
    public void VincularPoliticaBanco_TentaAlterarLimiteGlobalBancoId_LancaInvalidOperationException()
    {
        // Arrange
        Contrato contrato = CriarContratoValido();
        Guid limiteBancoId = Guid.NewGuid();
        Guid globalOriginal = Guid.NewGuid();
        Guid revisaoId = Guid.NewGuid();

        contrato.VincularPoliticaBanco(limiteBancoId, globalOriginal, revisaoId);

        Guid globalDiferente = Guid.NewGuid();

        // Act
        Action act = () => contrato.VincularPoliticaBanco(limiteBancoId, globalDiferente, revisaoId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LimiteGlobalBancoId*");
    }

    // ── SC-05 — imutabilidade: GarantiasExigidasRevisaoId não pode ser alterado ──

    [Fact]
    public void VincularPoliticaBanco_TentaAlterarGarantiasExigidasRevisaoId_LancaInvalidOperationException()
    {
        // Arrange
        Contrato contrato = CriarContratoValido();
        Guid limiteBancoId = Guid.NewGuid();
        Guid limiteGlobalId = Guid.NewGuid();
        Guid revisaoOriginal = Guid.NewGuid();

        contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalId, revisaoOriginal);

        Guid revisaoDiferente = Guid.NewGuid();

        // Act
        Action act = () => contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalId, revisaoDiferente);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GarantiasExigidasRevisaoId*");
    }

    // ── SC-06/SC-07 — contrato sem política vinculada é permitido ────────────

    [Fact]
    public void VincularPoliticaBanco_ComTodosNulls_Permitido()
    {
        // Arrange — banco sem LimiteBanco cadastrado (SC-07): todos os campos ficam nulos.
        Contrato contrato = CriarContratoValido();

        // Act
        Action act = () => contrato.VincularPoliticaBanco(null, null, null);

        // Assert
        act.Should().NotThrow();
        contrato.LimiteBancoId.Should().BeNull();
        contrato.LimiteGlobalBancoId.Should().BeNull();
        contrato.GarantiasExigidasRevisaoId.Should().BeNull();
    }

    // ── Preencher de null para non-null é permitido ──────────────────────────

    [Fact]
    public void VincularPoliticaBanco_DepoisDeNullParaValor_OK()
    {
        // Arrange — primeira chamada com todos nulos (estado sem política).
        Contrato contrato = CriarContratoValido();
        contrato.VincularPoliticaBanco(null, null, null);

        Guid limiteBancoId = Guid.NewGuid();
        Guid limiteGlobalId = Guid.NewGuid();
        Guid revisaoId = Guid.NewGuid();

        // Act — segunda chamada preenche valores sem erro.
        Action act = () => contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalId, revisaoId);

        // Assert
        act.Should().NotThrow();
        contrato.LimiteBancoId.Should().Be(limiteBancoId);
        contrato.LimiteGlobalBancoId.Should().Be(limiteGlobalId);
        contrato.GarantiasExigidasRevisaoId.Should().Be(revisaoId);
    }

    // ── SC-05 — Contrato.Atualizar não toca os campos de política ───────────

    [Fact]
    public void Atualizar_NaoMudaCamposDePoliticaBanco()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 25, 10, 0);
        Instant atualizacaoInstant = Instant.FromUtc(2026, 6, 1, 14, 0);

        Contrato contrato = CriarContratoValido();

        Guid limiteBancoId = Guid.NewGuid();
        Guid limiteGlobalId = Guid.NewGuid();
        Guid revisaoId = Guid.NewGuid();

        contrato.VincularPoliticaBanco(limiteBancoId, limiteGlobalId, revisaoId);

        // Act — Atualizar com dados arbitrários não deve tocar os 3 campos de política.
        contrato.Atualizar(
            clock: CriarClock(atualizacaoInstant),
            numeroExterno: "FIN-2026-SC05-EDITADO",
            observacoes: "Atualizado no teste");

        // Assert — campos de política permanecem inalterados.
        contrato.LimiteBancoId.Should().Be(limiteBancoId);
        contrato.LimiteGlobalBancoId.Should().Be(limiteGlobalId);
        contrato.GarantiasExigidasRevisaoId.Should().Be(revisaoId);

        // Confirma que Atualizar de fato modificou outros campos (teste útil).
        contrato.NumeroExterno.Should().Be("FIN-2026-SC05-EDITADO");
        contrato.UpdatedAt.Should().Be(atualizacaoInstant);
    }
}
