using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta o calendário de vencimentos de parcelas para um ano civil.
/// Converte valores de moeda estrangeira para BRL via spot ou PTAX D-1.
/// Meses sem parcelas são incluídos com valores zero.
/// </summary>
public sealed class GetCalendarioVencimentosQueryHandler(
    IContratoRepository contratoRepo,
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

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        contratos = AplicarFiltros(contratos, query);

        // Resolve cotações para moedas presentes nos contratos filtrados
        IReadOnlySet<Moeda> moedasEstrangeiras = contratos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasConversao =
            await ResolverTaxasAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Acumula por mês: (principal BRL, juros BRL, quantidade parcelas)
        decimal[] principaisPorMes = new decimal[13]; // índice 1..12
        decimal[] jurosPorMes = new decimal[13];
        int[] quantidadesPorMes = new int[13];

        foreach (Contrato contrato in contratos)
        {
            decimal taxa = contrato.Moeda == Moeda.Brl
                ? 1m
                : taxasConversao.TryGetValue(contrato.Moeda, out decimal t) ? t : 0m;

            foreach (Parcela parcela in contrato.Parcelas)
            {
                if (parcela.DataVencimento.Year != query.Ano)
                {
                    continue;
                }

                if (parcela.Status == StatusParcela.Paga)
                {
                    continue;
                }

                int mes = parcela.DataVencimento.Month;

                decimal principalBrl = Math.Round(
                    parcela.ValorPrincipal.Valor * taxa,
                    6,
                    MidpointRounding.AwayFromZero);

                decimal jurosBrl = Math.Round(
                    parcela.ValorJuros.Valor * taxa,
                    6,
                    MidpointRounding.AwayFromZero);

                principaisPorMes[mes] = Math.Round(
                    principaisPorMes[mes] + principalBrl,
                    6,
                    MidpointRounding.AwayFromZero);

                jurosPorMes[mes] = Math.Round(
                    jurosPorMes[mes] + jurosBrl,
                    6,
                    MidpointRounding.AwayFromZero);

                quantidadesPorMes[mes]++;
            }
        }

        List<MesVencimentoDto> meses = new(12);
        decimal totalAno = 0m;

        for (int mes = 1; mes <= 12; mes++)
        {
            decimal principalMes = Math.Round(principaisPorMes[mes], 2, MidpointRounding.AwayFromZero);
            decimal jurosMes = Math.Round(jurosPorMes[mes], 2, MidpointRounding.AwayFromZero);
            decimal totalMes = Math.Round(principalMes + jurosMes, 2, MidpointRounding.AwayFromZero);

            totalAno = Math.Round(totalAno + totalMes, 2, MidpointRounding.AwayFromZero);

            meses.Add(new MesVencimentoDto(
                Ano: query.Ano,
                Mes: mes,
                TotalPrincipalBrl: principalMes,
                TotalJurosBrl: jurosMes,
                TotalBrl: totalMes,
                QuantidadeParcelas: quantidadesPorMes[mes]));
        }

        return new CalendarioVencimentosDto(
            Ano: query.Ano,
            Meses: meses.AsReadOnly(),
            TotalAnoBrl: totalAno);
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
