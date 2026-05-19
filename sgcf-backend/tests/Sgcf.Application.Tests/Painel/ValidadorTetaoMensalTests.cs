using FluentAssertions;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes unitários para <see cref="ValidadorTetaoMensal"/>.
/// Fase 3 Task 3.4 — tetão mensal configurável (D-11).
///
/// ValidadorTetaoMensal é uma pure function: dado uma projeção e um valor de tetão,
/// retorna alertas para cada mês em que captações + amortizações excedem o limite.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ValidadorTetaoMensalTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Cria uma projeção mínima com os meses especificados.</summary>
    private static QuadroDividaProjecaoDto CriarProjecao(
        params (int Mes, decimal Amortizacao, decimal Captacao)[] meses)
    {
        List<MesProjecaoDto> mesDto = meses.Select(m =>
            new MesProjecaoDto(
                Ano: 2026,
                Mes: m.Mes,
                Bancos: [],
                SaldoTotalInicio: 1_000_000m,
                SaldoTotalFim: 1_000_000m - m.Amortizacao + m.Captacao,
                TotalAmortizacaoMes: m.Amortizacao,
                TotalCaptacaoMes: m.Captacao)).ToList();

        return new QuadroDividaProjecaoDto(mesDto.AsReadOnly());
    }

    // ── Teste 1: tetão não configurado (null) → nenhum alerta ────────────────

    [Fact]
    public void Validar_tetaoNaoConfigurado_naoAdicionaAlerta()
    {
        // Arrange — mês com movimentação alta, mas sem tetão configurado
        QuadroDividaProjecaoDto projecao = CriarProjecao(
            (Mes: 3, Amortizacao: 10_000_000m, Captacao: 5_000_000m));

        // Act
        IReadOnlyList<string> alertas = ValidadorTetaoMensal.Validar(projecao, tetaoBrl: null);

        // Assert
        alertas.Should().BeEmpty(
            "quando tetão não está configurado não há verificação de limite");
    }

    // ── Teste 2: tetão configurado, mês dentro do limite → nenhum alerta ─────

    [Fact]
    public void Validar_tetaoConfigurado_eDentroLimite_naoAdicionaAlerta()
    {
        // Arrange — amortizacao 800k + captacao 100k = 900k; tetão = 1M
        QuadroDividaProjecaoDto projecao = CriarProjecao(
            (Mes: 6, Amortizacao: 800_000m, Captacao: 100_000m));

        // Act
        IReadOnlyList<string> alertas = ValidadorTetaoMensal.Validar(projecao, tetaoBrl: 1_000_000m);

        // Assert
        alertas.Should().BeEmpty("900k < 1M: dentro do limite");
    }

    // ── Teste 3: tetão configurado, mês excede → alerta para esse mês ────────

    [Fact]
    public void Validar_tetaoConfigurado_eMesExcede_adicionaAlertaParaEsseMes()
    {
        // Arrange — amortizacao 600k + captacao 600k = 1.2M; tetão = 1M
        QuadroDividaProjecaoDto projecao = CriarProjecao(
            (Mes: 4, Amortizacao: 600_000m, Captacao: 600_000m));

        // Act
        IReadOnlyList<string> alertas = ValidadorTetaoMensal.Validar(projecao, tetaoBrl: 1_000_000m);

        // Assert
        alertas.Should().HaveCount(1, "exatamente um mês excedeu o tetão");
        alertas[0].Should().Contain("04/2026", "o alerta deve identificar mês e ano");
        alertas[0].Should().Contain("1.200.000", "o alerta deve informar a movimentação total");
        alertas[0].Should().Contain("1.000.000", "o alerta deve informar o tetão configurado");
    }

    // ── Teste 4: múltiplos meses excedendo → um alerta por mês ───────────────

    [Fact]
    public void Validar_multiplosMesesExcedendo_adicionaUmAlertaPorMes()
    {
        // Arrange — 3 meses excedendo, 1 dentro do limite
        QuadroDividaProjecaoDto projecao = CriarProjecao(
            (Mes: 1, Amortizacao: 600_000m, Captacao: 600_000m),   // 1.2M > 1M → alerta
            (Mes: 2, Amortizacao: 400_000m, Captacao: 400_000m),   // 800k < 1M → ok
            (Mes: 3, Amortizacao: 700_000m, Captacao: 500_000m),   // 1.2M > 1M → alerta
            (Mes: 4, Amortizacao: 800_000m, Captacao: 700_000m));  // 1.5M > 1M → alerta

        // Act
        IReadOnlyList<string> alertas = ValidadorTetaoMensal.Validar(projecao, tetaoBrl: 1_000_000m);

        // Assert
        alertas.Should().HaveCount(3, "cada mês que excede o tetão gera exatamente um alerta");
        alertas.Should().Contain(a => a.Contains("01/2026"), "janeiro excedeu");
        alertas.Should().Contain(a => a.Contains("03/2026"), "março excedeu");
        alertas.Should().Contain(a => a.Contains("04/2026"), "abril excedeu");
        alertas.Should().NotContain(a => a.Contains("02/2026"), "fevereiro não excedeu");
    }

    // ── Teste 5: exatamente no limite → não gera alerta (boundary) ───────────

    [Fact]
    public void Validar_movimentacaoExatamenteNoLimite_naoGerAlerta()
    {
        // Arrange — amortizacao 500k + captacao 500k = 1M = tetão → não excede
        QuadroDividaProjecaoDto projecao = CriarProjecao(
            (Mes: 7, Amortizacao: 500_000m, Captacao: 500_000m));

        // Act
        IReadOnlyList<string> alertas = ValidadorTetaoMensal.Validar(projecao, tetaoBrl: 1_000_000m);

        // Assert
        alertas.Should().BeEmpty("movimentação igual ao tetão não ultrapassa — sem alerta");
    }
}
