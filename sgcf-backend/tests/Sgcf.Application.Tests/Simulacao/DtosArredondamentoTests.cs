using FluentAssertions;

using NodaTime;
using NSubstitute;

using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Application.Tests.Simulacao;

/// <summary>
/// Verifica que os DTOs de saída do módulo Simulação aplicam arredondamento de
/// apresentação a 2 casas decimais (HalfUp) via <c>DecimalArredondamento.Mostrar</c>.
///
/// Motivação: valores monetários calculados internamente podem ter até 6 casas decimais
/// (via Money + projetor). A API devolve no máximo 2dp — regulação financeira BR.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DtosArredondamentoTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    // ── SimulacaoContratacaoDto ───────────────────────────────────────────────

    [Fact]
    public void SimulacaoContratacaoDto_From_ValorPrincipalComMaisDe2Casas_Arredonda()
    {
        // Arrange — valor com 6 casas decimais para forçar arredondamento HalfUp
        // O domínio armazena internamente a precisão completa do valor informado.
        // 100_000.456789m deve arredondar para 100_000.46m (HalfUp)
        SimulacaoContratacao simulacao = SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(100_000.456789m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(10m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Anual,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock(),
            anoBase: 2026);

        // Act
        SimulacaoContratacaoDto dto = SimulacaoContratacaoDto.From(simulacao);

        // Assert — 100000.456789 arredondado HalfUp = 100000.46
        dto.ValorPrincipal.Should().Be(100_000.46m,
            because: "ValorPrincipal deve ser arredondado para 2dp HalfUp na apresentação");
    }

    [Fact]
    public void SimulacaoContratacaoDto_From_ValorExato_NaoAltera()
    {
        // Arrange — valor já com 2dp (sem arredondamento necessário)
        SimulacaoContratacao simulacao = SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(500_000.50m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(12m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Anual,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock(),
            anoBase: 2026);

        // Act
        SimulacaoContratacaoDto dto = SimulacaoContratacaoDto.From(simulacao);

        // Assert — valor com 2dp não sofre alteração
        dto.ValorPrincipal.Should().Be(500_000.50m);
    }

    [Fact]
    public void SimulacaoContratacaoDto_From_HalfUp_NaoUsaBankersRounding()
    {
        // Arrange — 0.005 deve arredondar para 0.01 em HalfUp (não para 0.00 em ToEven)
        SimulacaoContratacao simulacao = SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000.005m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(10m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Anual,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock(),
            anoBase: 2026);

        // Act
        SimulacaoContratacaoDto dto = SimulacaoContratacaoDto.From(simulacao);

        // Assert — HalfUp: 1000.005 → 1000.01 (não 1000.00 como faria ToEven/BankersRounding)
        dto.ValorPrincipal.Should().Be(1_000.01m,
            because: "regulação BR exige HalfUp, não ToEven (banker's rounding)");
    }
}
