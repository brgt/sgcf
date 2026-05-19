using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Projeta o saldo mensal por banco a partir de uma posição inicial e
/// uma lista de eventos (amortizações de principal + captações).
///
/// Função pura (AD-5): mesma entrada → mesma saída. Sem I/O, sem clock, sem state mutável.
///
/// Invariantes garantidas (SPEC §6.4):
///   P-1: SaldoFim[m, banco] == SaldoInicio[m+1, banco] para todo banco e m em 1..11.
///   P-2: SaldoTotalFim[m] == Σ SaldoFim[m, *].
///   P-3: |Σ SharePercentual[m, *] − 100| &lt; 0,01 quando SaldoTotalFim[m] &gt; 0.
///   P-4: Banco sem saldo inicial mas com captação é incluído a partir do mês da captação.
///   P-5: Eventos com Data.Year != ano são ignorados (não é erro).
///   P-6: Banco sem saldo inicial e sem eventos não aparece no resultado.
/// </summary>
public static class ProjetorSaldoMensal
{
    private const int MesesNoAno = 12;
    private const int CasasDecimaisShare = 4;

    /// <summary>
    /// Projeta saldo mensal por banco para os 12 meses do ano informado.
    /// </summary>
    /// <param name="saldoInicialPorBanco">
    /// Saldo no primeiro dia do ano por banco (BancoId → Money em BRL).
    /// Representa a posição real no início do período projetado.
    /// </param>
    /// <param name="eventos">
    /// Lista de eventos de amortização de principal e captação dentro (ou fora) do ano.
    /// Eventos com <c>Data.Year != ano</c> são silenciosamente ignorados (P-5).
    /// </param>
    /// <param name="ano">Ano civil a projetar (aceito entre 2020 e 2100 inclusive).</param>
    /// <returns>
    /// <see cref="QuadroDividaProjecao"/> com exatamente 12 <see cref="MesProjecao"/>.
    /// </returns>
    public static QuadroDividaProjecao Projetar(
        IReadOnlyDictionary<Guid, Money> saldoInicialPorBanco,
        IReadOnlyList<EventoProjecao> eventos,
        int ano)
    {
        ArgumentNullException.ThrowIfNull(saldoInicialPorBanco);
        ArgumentNullException.ThrowIfNull(eventos);

        if (ano < 2020 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano,
                "Ano deve estar entre 2020 e 2100.");
        }

        // Filtra apenas os eventos do ano solicitado (P-5)
        List<EventoProjecao> eventosFiltrados = [.. eventos.Where(e => e.Data.Year == ano)];

        // Bancos envolvidos = saldo inicial + bancos que aparecem em eventos do ano (P-4 e P-6)
        HashSet<Guid> bancosEnvolvidos = [
            .. saldoInicialPorBanco.Keys,
            .. eventosFiltrados.Select(e => e.BancoId),
        ];

        // Saldo corrente por banco ao longo dos meses (mutable apenas dentro deste método)
        // Inicializado com saldo zero para bancos que só aparecem em eventos (P-4)
        Dictionary<Guid, decimal> saldoCorrentePorBanco = bancosEnvolvidos.ToDictionary(
            id => id,
            id => saldoInicialPorBanco.TryGetValue(id, out Money m) ? m.Valor : 0m);

        decimal saldoInicialTotal = saldoCorrentePorBanco.Values.Sum();
        Money saldoInicialTotalBrl = new(saldoInicialTotal, Moeda.Brl);

        var mesesProjetados = new List<MesProjecao>(MesesNoAno);

        for (int mes = 1; mes <= MesesNoAno; mes++)
        {
            MesProjecao mesProjetado = ProjetarMes(
                mes,
                ano,
                bancosEnvolvidos,
                saldoCorrentePorBanco,
                eventosFiltrados);

            mesesProjetados.Add(mesProjetado);

            // Atualiza o saldo corrente para que o próximo mês parta de SaldoFim (P-1)
            foreach (SaldoBancoMes saldoBanco in mesProjetado.SaldosPorBanco)
            {
                saldoCorrentePorBanco[saldoBanco.BancoId] = saldoBanco.SaldoFim.Valor;
            }
        }

        return new QuadroDividaProjecao(ano, mesesProjetados.AsReadOnly(), saldoInicialTotalBrl);
    }

    /// <summary>
    /// Projeta um único mês e devolve o <see cref="MesProjecao"/> resultante.
    /// Atualiza <paramref name="saldoCorrentePorBanco"/> como efeito colateral intencional
    /// — isso é controlado exclusivamente dentro do método <see cref="Projetar"/>.
    /// </summary>
    private static MesProjecao ProjetarMes(
        int mes,
        int ano,
        HashSet<Guid> bancosEnvolvidos,
        Dictionary<Guid, decimal> saldoCorrentePorBanco,
        List<EventoProjecao> eventosFiltrados)
    {
        // Indexa eventos do mês atual por banco para acesso O(1)
        var amortizacoesPorBanco = IndexarEventosPorBanco(
            eventosFiltrados, mes, TipoEventoProjecao.AmortizacaoPrincipal);
        var captacoesPorBanco = IndexarEventosPorBanco(
            eventosFiltrados, mes, TipoEventoProjecao.Captacao);

        // Bancos ativos neste mês = todos com saldo não-zero OU com eventos no mês (P-4 e P-6)
        HashSet<Guid> bancosAtivosNoMes = [
            .. bancosEnvolvidos.Where(id =>
                saldoCorrentePorBanco.TryGetValue(id, out decimal s) && s != 0m
                || amortizacoesPorBanco.ContainsKey(id)
                || captacoesPorBanco.ContainsKey(id)),
        ];

        // Calcula SaldoFim de cada banco ativo
        Dictionary<Guid, (decimal saldoInicio, decimal totalAmort, decimal totalCap, decimal saldoFim)> calculos =
            new(bancosAtivosNoMes.Count);

        foreach (Guid bancoId in bancosAtivosNoMes)
        {
            decimal saldoInicio = saldoCorrentePorBanco.TryGetValue(bancoId, out decimal s) ? s : 0m;

            decimal totalAmort = amortizacoesPorBanco.TryGetValue(bancoId, out decimal a) ? a : 0m;
            decimal totalCap = captacoesPorBanco.TryGetValue(bancoId, out decimal c) ? c : 0m;

            decimal saldoFim = Arredondamento.HalfUp(saldoInicio - totalAmort + totalCap, casas: 6);

            calculos[bancoId] = (saldoInicio, totalAmort, totalCap, saldoFim);
        }

        // Saldo total de fechamento necessário para calcular shares (P-2 e P-3)
        decimal saldoTotalFim = calculos.Values.Sum(c => c.saldoFim);
        decimal saldoTotalInicio = calculos.Values.Sum(c => c.saldoInicio);

        // Monta os registros por banco com share calculado
        List<SaldoBancoMes> saldosPorBanco = new(bancosAtivosNoMes.Count);

        foreach ((Guid bancoId, (decimal saldoInicio, decimal totalAmort, decimal totalCap, decimal saldoFim)) in calculos)
        {
            decimal share = CalcularShare(saldoFim, saldoTotalFim);

            saldosPorBanco.Add(new SaldoBancoMes(
                BancoId: bancoId,
                SaldoInicio: new Money(saldoInicio, Moeda.Brl),
                SaldoFim: new Money(saldoFim, Moeda.Brl),
                TotalAmortizacaoNoMes: new Money(totalAmort, Moeda.Brl),
                TotalCaptacaoNoMes: new Money(totalCap, Moeda.Brl),
                SharePercentual: share));
        }

        return new MesProjecao(
            AnoCalendar: ano,
            Mes: mes,
            SaldosPorBanco: saldosPorBanco.AsReadOnly(),
            SaldoTotalInicio: new Money(saldoTotalInicio, Moeda.Brl),
            SaldoTotalFim: new Money(saldoTotalFim, Moeda.Brl));
    }

    /// <summary>
    /// Indexa os valores totais de eventos de um determinado tipo por banco e mês.
    /// Eventos do mesmo banco e tipo no mesmo mês são somados (P da SPEC §6.4).
    /// </summary>
    private static Dictionary<Guid, decimal> IndexarEventosPorBanco(
        List<EventoProjecao> eventos,
        int mes,
        TipoEventoProjecao tipo)
    {
        var resultado = new Dictionary<Guid, decimal>();

        foreach (EventoProjecao evento in eventos)
        {
            if (evento.Data.Month != mes || evento.Tipo != tipo)
            {
                continue;
            }

            if (resultado.TryGetValue(evento.BancoId, out decimal acumulado))
            {
                resultado[evento.BancoId] = Arredondamento.HalfUp(acumulado + evento.ValorBrl.Valor, casas: 6);
            }
            else
            {
                resultado[evento.BancoId] = evento.ValorBrl.Valor;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Calcula o share percentual de um banco.
    /// Retorna 0 quando o saldo total for zero para evitar divisão por zero (P-3).
    /// Arredondado HalfUp a 4 casas decimais.
    /// </summary>
    private static decimal CalcularShare(decimal saldoFimBanco, decimal saldoTotalFim)
    {
        if (saldoTotalFim == 0m)
        {
            return 0m;
        }

        decimal share = saldoFimBanco / saldoTotalFim * 100m;
        return Arredondamento.HalfUp(share, casas: CasasDecimaisShare);
    }
}
