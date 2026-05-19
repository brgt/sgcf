using FluentAssertions;

using FsCheck;
using FsCheck.Xunit;

using NodaTime;

using Sgcf.Domain.Common;
using Sgcf.Domain.Painel;

using Xunit;

namespace Sgcf.Domain.Tests.Painel;

/// <summary>
/// Testes unitários + property-based para <see cref="ProjetorSaldoMensal"/>.
/// Cobre os 8 cenários unitários obrigatórios e as 2 propriedades FsCheck (P-1 e P-3 da SPEC §6.4).
/// </summary>
[Trait("Category", "Domain")]
public sealed class ProjetorSaldoMensalTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static readonly int AnoBase = 2026;

    private static Money Brl(decimal valor) => new(valor, Moeda.Brl);

    private static EventoProjecao Amortizacao(Guid bancoId, int mes, decimal valor) =>
        new(bancoId, new LocalDate(AnoBase, mes, 15), TipoEventoProjecao.AmortizacaoPrincipal, Brl(valor));

    private static EventoProjecao Captacao(Guid bancoId, int mes, decimal valor) =>
        new(bancoId, new LocalDate(AnoBase, mes, 10), TipoEventoProjecao.Captacao, Brl(valor));

    // ── Teste 1: sem eventos → 12 meses com saldo igual ao inicial ───────────

    [Fact]
    public void Projetar_SemEventos_Retorna12MesesComSaldoIgualAoInicial()
    {
        // Arrange
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(1_000_000m) };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, [], AnoBase);

        // Assert
        resultado.Meses.Should().HaveCount(12);

        foreach (MesProjecao mes in resultado.Meses)
        {
            mes.SaldosPorBanco.Should().HaveCount(1);
            SaldoBancoMes saldo = mes.SaldosPorBanco[0];
            saldo.BancoId.Should().Be(bancoId);
            saldo.SaldoInicio.Valor.Should().Be(1_000_000m);
            saldo.SaldoFim.Valor.Should().Be(1_000_000m);
            saldo.TotalAmortizacaoNoMes.Valor.Should().Be(0m);
            saldo.TotalCaptacaoNoMes.Valor.Should().Be(0m);
        }
    }

    // ── Teste 2: amortização no mês 3 → saldo reduz a partir do mês 3 ───────

    [Fact]
    public void Projetar_ComAmortizacaoEmMes3_ReduzSaldoApartirDoMes3()
    {
        // Arrange
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(500_000m) };
        var eventos = new List<EventoProjecao> { Amortizacao(bancoId, 3, 100_000m) };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert
        resultado.Meses[0].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(500_000m); // mês 1 inalterado
        resultado.Meses[1].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(500_000m); // mês 2 inalterado
        resultado.Meses[2].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(400_000m); // mês 3 reduz
        resultado.Meses[3].SaldosPorBanco[0].SaldoInicio.Valor.Should().Be(400_000m); // mês 4 continua reduzido
        resultado.Meses[11].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(400_000m); // mês 12 mantém
    }

    // ── Teste 3: captação no mês 6 → saldo aumenta a partir do mês 6 ────────

    [Fact]
    public void Projetar_ComCaptacaoEmMes6_AumentaSaldoApartirDoMes6()
    {
        // Arrange
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(200_000m) };
        var eventos = new List<EventoProjecao> { Captacao(bancoId, 6, 50_000m) };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert
        resultado.Meses[4].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(200_000m); // mês 5 inalterado
        resultado.Meses[5].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(250_000m); // mês 6 aumenta
        resultado.Meses[5].SaldosPorBanco[0].TotalCaptacaoNoMes.Valor.Should().Be(50_000m);
        resultado.Meses[11].SaldosPorBanco[0].SaldoFim.Valor.Should().Be(250_000m); // mês 12 mantém
    }

    // ── Teste 4: evento fora do ano é ignorado ────────────────────────────────

    [Fact]
    public void Projetar_EventoForaDoAno_EIgnorado()
    {
        // Arrange — evento em 2025 (ano anterior) e em 2027 (ano seguinte)
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(300_000m) };
        var eventos = new List<EventoProjecao>
        {
            new(bancoId, new LocalDate(2025, 6, 15), TipoEventoProjecao.AmortizacaoPrincipal, Brl(100_000m)),
            new(bancoId, new LocalDate(2027, 3, 10), TipoEventoProjecao.Captacao, Brl(80_000m)),
        };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert — saldo permanece inalterado em todos os meses
        foreach (MesProjecao mes in resultado.Meses)
        {
            mes.SaldosPorBanco[0].SaldoFim.Valor.Should().Be(300_000m);
        }
    }

    // ── Teste 5: múltiplos bancos → share calculado por banco por mês ─────────

    [Fact]
    public void Projetar_MultiplosBancos_CalculaSharePorBancoPorMes()
    {
        // Arrange — dois bancos com saldo 60/40
        var bancoA = Guid.NewGuid();
        var bancoB = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money>
        {
            [bancoA] = Brl(600_000m),
            [bancoB] = Brl(400_000m),
        };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, [], AnoBase);

        // Assert — share deve ser 60% e 40% em todos os meses (sem eventos)
        foreach (MesProjecao mes in resultado.Meses)
        {
            mes.SaldoTotalFim.Valor.Should().Be(1_000_000m);

            SaldoBancoMes saldoA = mes.SaldosPorBanco.Single(s => s.BancoId == bancoA);
            SaldoBancoMes saldoB = mes.SaldosPorBanco.Single(s => s.BancoId == bancoB);

            // Tolerância para arredondamento: 0,0001 pp
            saldoA.SharePercentual.Should().BeApproximately(60m, precision: 0.0001m);
            saldoB.SharePercentual.Should().BeApproximately(40m, precision: 0.0001m);

            // Soma dos shares deve fechar em 100%
            (saldoA.SharePercentual + saldoB.SharePercentual).Should().BeApproximately(100m, precision: 0.01m);
        }
    }

    // ── Teste 6: amortização igual ao saldo → reduz para zero no mês ─────────

    [Fact]
    public void Projetar_AmortizacaoIguaisAoSaldo_ReduzPara0NoMes()
    {
        // Arrange
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(150_000m) };
        var eventos = new List<EventoProjecao> { Amortizacao(bancoId, 4, 150_000m) };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert — mês 4: banco aparece com saldo zero após amortização total
        SaldoBancoMes mes4 = resultado.Meses[3].SaldosPorBanco[0];
        mes4.SaldoFim.Valor.Should().Be(0m);
        mes4.TotalAmortizacaoNoMes.Valor.Should().Be(150_000m);

        // Meses seguintes: banco com saldo zero sem eventos não aparece em SaldosPorBanco (P-6)
        for (int m = 4; m < 12; m++)
        {
            resultado.Meses[m].SaldosPorBanco.Should().BeEmpty(
                because: "banco com saldo zero e sem eventos não aparece no resultado (SPEC P-6)");
        }
    }

    // ── Teste 7: saldo de fechamento no mês 12 é calculado corretamente ───────

    [Fact]
    public void Projetar_CapturaSaldoFimMes12_Correto()
    {
        // Arrange — amortizações mensais fixas de 10.000 ao longo do ano
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(120_000m) };

        var eventos = Enumerable.Range(1, 12)
            .Select(m => Amortizacao(bancoId, m, 10_000m))
            .ToList();

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert — saldo final no mês 12 deve ser 0
        MesProjecao mes12 = resultado.Meses[11];
        mes12.SaldosPorBanco[0].SaldoInicio.Valor.Should().Be(10_000m);
        mes12.SaldosPorBanco[0].TotalAmortizacaoNoMes.Valor.Should().Be(10_000m);
        mes12.SaldosPorBanco[0].SaldoFim.Valor.Should().Be(0m);
        mes12.SaldoTotalFim.Valor.Should().Be(0m);
    }

    // ── Teste 8: eventos no mesmo banco e mesmo mês são somados ──────────────

    [Fact]
    public void Projetar_EventosNoMesmoBancoEMesmoMes_Somam()
    {
        // Arrange — três amortizações no banco X no mês 5
        var bancoId = Guid.NewGuid();
        var saldoInicial = new Dictionary<Guid, Money> { [bancoId] = Brl(500_000m) };
        var eventos = new List<EventoProjecao>
        {
            Amortizacao(bancoId, 5, 30_000m),
            Amortizacao(bancoId, 5, 20_000m),
            Amortizacao(bancoId, 5, 10_000m),
        };

        // Act
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(saldoInicial, eventos, AnoBase);

        // Assert — amortizações somadas = 60.000
        SaldoBancoMes mes5 = resultado.Meses[4].SaldosPorBanco[0];
        mes5.TotalAmortizacaoNoMes.Valor.Should().Be(60_000m);
        mes5.SaldoFim.Valor.Should().Be(440_000m);
    }

    // ── Property 1: SaldoFimMes[N] == SaldoInicioMes[N+1] por banco (P-1) ────

    [Property(MaxTest = 200, Arbitrary = [typeof(ArbitraryProjecao)])]
    public Property Property_SaldoFimMes_N_IgualSaldoInicioMes_N_Mais_1(
        ProjecaoInput input)
    {
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(
            input.SaldoInicial,
            input.Eventos,
            AnoBase);

        // Para cada banco, verifica continuidade entre meses consecutivos
        for (int m = 0; m < 11; m++)
        {
            MesProjecao mesAtual = resultado.Meses[m];
            MesProjecao mesProximo = resultado.Meses[m + 1];

            foreach (SaldoBancoMes saldoAtual in mesAtual.SaldosPorBanco)
            {
                SaldoBancoMes? saldoProximo = mesProximo.SaldosPorBanco
                    .FirstOrDefault(s => s.BancoId == saldoAtual.BancoId);

                if (saldoProximo is null)
                {
                    // Banco com saldo zero pode não aparecer no próximo mês — aceito
                    if (saldoAtual.SaldoFim.Valor != 0m)
                    {
                        return false.ToProperty();
                    }

                    continue;
                }

                if (saldoAtual.SaldoFim.Valor != saldoProximo.SaldoInicio.Valor)
                {
                    return false.ToProperty();
                }
            }
        }

        return true.ToProperty();
    }

    // ── Property 2: Σ(SharePercentual) == 100 quando totalFim > 0 (P-3) ──────

    [Property(MaxTest = 200, Arbitrary = [typeof(ArbitraryProjecao)])]
    public Property Property_SomaSharesPorMes_Igual_100_QuandoTotalNaoZero(
        ProjecaoInput input)
    {
        QuadroDividaProjecao resultado = ProjetorSaldoMensal.Projetar(
            input.SaldoInicial,
            input.Eventos,
            AnoBase);

        foreach (MesProjecao mes in resultado.Meses)
        {
            if (mes.SaldoTotalFim.Valor <= 0m)
            {
                continue; // share é zero quando saldo total é zero — não aplica a propriedade
            }

            decimal somaShares = mes.SaldosPorBanco.Sum(s => s.SharePercentual);

            // Tolerância de 0,01 pp conforme SPEC §6.4 P-3
            if (Math.Abs(somaShares - 100m) > 0.01m)
            {
                return false.ToProperty();
            }
        }

        return true.ToProperty();
    }
}

// ── Tipos e geradores para FsCheck ──────────────────────────────────────────

/// <summary>Input gerado randomicamente para os property tests.</summary>
public sealed record ProjecaoInput(
    IReadOnlyDictionary<Guid, Money> SaldoInicial,
    IReadOnlyList<EventoProjecao> Eventos);

/// <summary>Geradores FsCheck para <see cref="ProjecaoInput"/>.</summary>
public static class ArbitraryProjecao
{
    private static readonly int AnoBase = 2026;

    /// <summary>
    /// Gera um <see cref="ProjecaoInput"/> com 1 a 3 bancos, saldo inicial entre 0 e 1M,
    /// e 0 a 6 eventos aleatórios dentro do ano 2026.
    /// </summary>
    public static Arbitrary<ProjecaoInput> ProjecaoInputArbitrary()
    {
        Gen<Guid> guidGen = Gen.Fresh(Guid.NewGuid);

        Gen<ProjecaoInput> gen = from numBancos in Gen.Choose(1, 3)
                                 from bancoIdsFSharp in Gen.ListOf(numBancos, guidGen)
                                 let bancoIds = bancoIdsFSharp.ToList()
                                 from saldos in GenSaldosPorBanco(bancoIds)
                                 from numEventos in Gen.Choose(0, 6)
                                 from eventos in GenEventos(bancoIds, numEventos)
                                 select new ProjecaoInput(
                                     saldos.ToDictionary(kv => kv.Key, kv => kv.Value),
                                     eventos);

        return gen.ToArbitrary();
    }

    private static Gen<IEnumerable<KeyValuePair<Guid, Money>>> GenSaldosPorBanco(List<Guid> bancoIds) =>
        Gen.Sequence(bancoIds.Select(id =>
            from valor in Gen.Choose(0, 1_000_000)
            select new KeyValuePair<Guid, Money>(id, new Money((decimal)valor, Moeda.Brl))));

    private static Gen<IReadOnlyList<EventoProjecao>> GenEventos(List<Guid> bancoIds, int numEventos)
    {
        if (numEventos == 0)
        {
            return Gen.Constant((IReadOnlyList<EventoProjecao>)Array.Empty<EventoProjecao>());
        }

        Gen<EventoProjecao> eventoGen =
            from bancoIdx in Gen.Choose(0, bancoIds.Count - 1)
            from mes in Gen.Choose(1, 12)
            from tipo in Gen.Elements(TipoEventoProjecao.AmortizacaoPrincipal, TipoEventoProjecao.Captacao)
            from valor in Gen.Choose(1, 200_000)
            select new EventoProjecao(
                bancoIds[bancoIdx],
                new LocalDate(AnoBase, mes, 15),
                tipo,
                new Money((decimal)valor, Moeda.Brl));

        return Gen.ListOf(numEventos, eventoGen)
                  .Select(l => (IReadOnlyList<EventoProjecao>)l.ToList());
    }
}
