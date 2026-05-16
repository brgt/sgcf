using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Cronograma;
using System.Collections.ObjectModel;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta o calendário de vencimentos para um ano civil lendo EventoCronograma
/// (tabela cronograma_pagamento). Converte valores em moeda estrangeira para BRL
/// via spot ou PTAX D-1. Meses sem eventos são incluídos com valores zero.
/// </summary>
public sealed class GetCalendarioVencimentosQueryHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetCalendarioVencimentosQuery, CalendarioVencimentosDto>
{
    public async Task<CalendarioVencimentosDto> Handle(
        GetCalendarioVencimentosQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;

        // Carrega contratos apenas para aplicar os filtros de banco/modalidade/moeda
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        contratos = AplicarFiltros(contratos, query);

        if (contratos.Count == 0)
        {
            return ConstruirVazio(query.Ano);
        }

        IReadOnlyList<Guid> contratoIds = contratos.Select(c => c.Id).ToList().AsReadOnly();

        // Mapa para conversão: moeda → taxa BRL
        IReadOnlySet<Moeda> moedasEstrangeiras = contratos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasConversao =
            await ResolverTaxasAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Mapas contratoId → moeda e → número de contrato
        Dictionary<Guid, Moeda> moedaPorContrato = contratos.ToDictionary(c => c.Id, c => c.Moeda);
        Dictionary<Guid, string> numeroPorContrato = contratos.ToDictionary(c => c.Id, c => c.NumeroExterno);

        // Busca eventos abertos do ano diretamente na tabela de cronograma
        IReadOnlyList<EventoCronograma> eventos =
            await cronogramaRepo.ListAbertosParaAnoAsync(query.Ano, contratoIds, cancellationToken);

        // Agrupa por (mês, data, contratoId) para combinar Principal + Juros do mesmo vencimento
        // Chave: (mes, dataPrevista, contratoId)
        var acumulado = new Dictionary<(int Mes, LocalDate Data, Guid ContratoId), (decimal Principal, decimal Juros)>();

        foreach (EventoCronograma evento in eventos)
        {
            if (evento.Tipo != TipoEventoCronograma.Principal
                && evento.Tipo != TipoEventoCronograma.Juros)
            {
                continue;
            }

            Moeda moeda = moedaPorContrato.TryGetValue(evento.ContratoId, out Moeda m) ? m : evento.Moeda;
            decimal taxa = moeda == Moeda.Brl
                ? 1m
                : taxasConversao.TryGetValue(moeda, out decimal t) ? t : 0m;

            decimal valorBrl = Math.Round(evento.ValorMoedaOriginal.Valor * taxa, 6, MidpointRounding.AwayFromZero);
            var chave = (evento.DataPrevista.Month, evento.DataPrevista, evento.ContratoId);

            if (!acumulado.TryGetValue(chave, out var totais))
            {
                totais = (0m, 0m);
            }

            acumulado[chave] = evento.Tipo == TipoEventoCronograma.Principal
                ? (Math.Round(totais.Principal + valorBrl, 6, MidpointRounding.AwayFromZero), totais.Juros)
                : (totais.Principal, Math.Round(totais.Juros + valorBrl, 6, MidpointRounding.AwayFromZero));
        }

        // CDI flat projection — only when caller provides the current CDI rate
        Dictionary<(int Mes, LocalDate Data, Guid ContratoId), decimal>? projecaoJuros = null;
        if (query.CdiAnualPct.HasValue)
        {
            projecaoJuros = await CalcularProjecaoCdiAsync(
                eventos, contratos, moedaPorContrato, taxasConversao, query.CdiAnualPct.Value, cancellationToken);
        }

        // Organiza os itens por mês, ordenados por data
        var itensPorMes = new Dictionary<int, List<VencimentoItemDto>>();
        for (int mes = 1; mes <= 12; mes++)
        {
            itensPorMes[mes] = [];
        }

        foreach (var ((mes, data, contratoId), (principal, juros)) in acumulado.OrderBy(kv => kv.Key.Data))
        {
            decimal principalArred = Math.Round(principal, 2, MidpointRounding.AwayFromZero);
            decimal jurosArred = Math.Round(juros, 2, MidpointRounding.AwayFromZero);
            string numeroContrato = numeroPorContrato.TryGetValue(contratoId, out string? n) ? n : contratoId.ToString();

            decimal? jurosProjetadoArred = null;
            if (projecaoJuros is not null
                && projecaoJuros.TryGetValue((mes, data, contratoId), out decimal jp))
            {
                jurosProjetadoArred = Math.Round(jp, 2, MidpointRounding.AwayFromZero);
            }

            itensPorMes[mes].Add(new VencimentoItemDto(
                Data: data.ToString("yyyy-MM-dd", null),
                ContratoId: contratoId,
                NumeroContrato: numeroContrato,
                PrincipalBrl: principalArred,
                JurosBrl: jurosArred,
                TotalBrl: Math.Round(principalArred + jurosArred, 2, MidpointRounding.AwayFromZero),
                JurosBrlProjetado: jurosProjetadoArred));
        }

        List<MesVencimentoDto> meses = new(12);
        decimal totalAno = 0m;

        for (int mes = 1; mes <= 12; mes++)
        {
            System.Collections.ObjectModel.ReadOnlyCollection<VencimentoItemDto> itens = itensPorMes[mes].AsReadOnly();
            decimal principalMes = Math.Round(itens.Sum(i => i.PrincipalBrl), 2, MidpointRounding.AwayFromZero);
            decimal jurosMes = Math.Round(itens.Sum(i => i.JurosBrl), 2, MidpointRounding.AwayFromZero);
            decimal totalMes = Math.Round(principalMes + jurosMes, 2, MidpointRounding.AwayFromZero);

            totalAno = Math.Round(totalAno + totalMes, 2, MidpointRounding.AwayFromZero);

            decimal? totalJurosProjetadoMes = projecaoJuros is not null
                ? Math.Round(itens.Sum(i => i.JurosBrlProjetado ?? 0m), 2, MidpointRounding.AwayFromZero)
                : null;

            meses.Add(new MesVencimentoDto(
                Ano: query.Ano,
                Mes: mes,
                TotalPrincipalBrl: principalMes,
                TotalJurosBrl: jurosMes,
                TotalBrl: totalMes,
                QuantidadeParcelas: itens.Count,
                Parcelas: itens,
                TotalJurosBrlProjetado: totalJurosProjetadoMes));
        }

        return new CalendarioVencimentosDto(
            Ano: query.Ano,
            Meses: meses.AsReadOnly(),
            TotalAnoBrl: totalAno,
            TaxaCdiUsadaPct: query.CdiAnualPct);
    }

    private static CalendarioVencimentosDto ConstruirVazio(int ano)
    {
        List<MesVencimentoDto> meses = Enumerable.Range(1, 12)
            .Select(m => new MesVencimentoDto(ano, m, 0m, 0m, 0m, 0, Array.Empty<VencimentoItemDto>()))
            .ToList();
        return new CalendarioVencimentosDto(ano, meses.AsReadOnly(), 0m);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<Contrato> AplicarFiltros(
        IReadOnlyList<Contrato> contratos,
        GetCalendarioVencimentosQuery query)
    {
        IEnumerable<Contrato> resultado = contratos;

        if (query.BancoId.HasValue)
        {
            resultado = resultado.Where(c => c.BancoId == query.BancoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Modalidade)
            && Enum.TryParse<ModalidadeContrato>(query.Modalidade, ignoreCase: true, out ModalidadeContrato modalidade))
        {
            resultado = resultado.Where(c => c.Modalidade == modalidade);
        }

        if (!string.IsNullOrWhiteSpace(query.Moeda)
            && Enum.TryParse<Moeda>(query.Moeda, ignoreCase: true, out Moeda moedaFiltro))
        {
            resultado = resultado.Where(c => c.Moeda == moedaFiltro);
        }

        return resultado.ToList().AsReadOnly();
    }

    private async Task<Dictionary<(int Mes, LocalDate Data, Guid ContratoId), decimal>> CalcularProjecaoCdiAsync(
        IReadOnlyList<EventoCronograma> eventos,
        IReadOnlyList<Contrato> contratos,
        Dictionary<Guid, Moeda> moedaPorContrato,
        Dictionary<Moeda, decimal> taxasConversao,
        decimal cdiAnualPct,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> contratosComJurosZero = eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Juros && e.ValorMoedaOriginal.Valor == 0m)
            .Select(e => e.ContratoId)
            .ToHashSet();

        var resultado = new Dictionary<(int, LocalDate, Guid), decimal>();

        if (contratosComJurosZero.Count == 0)
        {
            return resultado;
        }

        IReadOnlyList<EventoCronograma> todosPrincipais =
            await cronogramaRepo.ListPrincipaisOrdenadosByContratoIdsAsync(contratosComJurosZero, cancellationToken);

        Dictionary<Guid, List<EventoCronograma>> principaisPorContrato = todosPrincipais
            .GroupBy(e => e.ContratoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<Guid, Contrato> contratoPorId = contratos
            .Where(c => contratosComJurosZero.Contains(c.Id))
            .ToDictionary(c => c.Id);

        foreach (EventoCronograma evento in eventos)
        {
            if (evento.Tipo != TipoEventoCronograma.Juros || evento.ValorMoedaOriginal.Valor != 0m)
            {
                continue;
            }

            if (!contratoPorId.TryGetValue(evento.ContratoId, out Contrato? contrato))
            {
                continue;
            }

            decimal saldoAntes = contrato.ValorPrincipal.Valor;
            LocalDate dataAnterior = contrato.DataContratacao;

            if (principaisPorContrato.TryGetValue(evento.ContratoId, out List<EventoCronograma>? principais))
            {
                foreach (EventoCronograma p in principais)
                {
                    if (p.DataPrevista >= evento.DataPrevista)
                    {
                        break;
                    }

                    saldoAntes -= p.ValorMoedaOriginal.Valor;
                    dataAnterior = p.DataPrevista;
                }
            }

            if (saldoAntes <= 0m)
            {
                continue;
            }

            int dias = Period.Between(dataAnterior, evento.DataPrevista, PeriodUnits.Days).Days;
            if (dias <= 0)
            {
                continue;
            }

            decimal taxaAnualPct = cdiAnualPct + contrato.TaxaAa.AsHumano;
            decimal fator = (decimal)Math.Pow(
                (double)(1m + taxaAnualPct / 100m),
                dias / (double)contrato.BaseCalculo);
            decimal jurosProjetado = Math.Round(saldoAntes * (fator - 1m), 6, MidpointRounding.AwayFromZero);

            Moeda moeda = moedaPorContrato.TryGetValue(evento.ContratoId, out Moeda m) ? m : evento.Moeda;
            decimal taxa = moeda == Moeda.Brl ? 1m : taxasConversao.TryGetValue(moeda, out decimal t) ? t : 0m;
            decimal jurosProjetadoBrl = Math.Round(jurosProjetado * taxa, 6, MidpointRounding.AwayFromZero);

            var chave = (evento.DataPrevista.Month, evento.DataPrevista, evento.ContratoId);
            resultado[chave] = resultado.TryGetValue(chave, out decimal existente)
                ? Math.Round(existente + jurosProjetadoBrl, 6, MidpointRounding.AwayFromZero)
                : jurosProjetadoBrl;
        }

        return resultado;
    }

    private async Task<Dictionary<Moeda, decimal>> ResolverTaxasAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> resultado = new();

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = spot.Value.Valor;
                continue;
            }

            CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                resultado[moeda] = midRate;
            }
        }

        return resultado;
    }
}
