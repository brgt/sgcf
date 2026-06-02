using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Agrega a dívida ativa por <see cref="ModalidadeContrato"/>, converte cada grupo para BRL
/// e calcula o percentual de participação de cada modalidade no total da carteira.
///
/// <para>
/// Estratégia de cotação idêntica ao <see cref="GetPainelDividaQueryHandler"/>:
/// spot intraday via Redis; na ausência, PTAX D-1 mid-rate ((compra+venda)/2);
/// se nenhuma cotação disponível, a modalidade contribui com zero BRL.
/// </para>
/// </summary>
public sealed class GetBreakdownModalidadeQueryHandler(
    IContratoRepository contratoRepo,
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock)
    : IRequestHandler<GetBreakdownModalidadeQuery, EnvelopeResponse<BreakdownModalidadeDto>>
{
    public async Task<EnvelopeResponse<BreakdownModalidadeDto>> Handle(
        GetBreakdownModalidadeQuery query,
        CancellationToken cancellationToken)
    {
        // Calendário brasileiro derivado do fuso BRT — nunca UTC.
        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        // O repositório aplica o global filter de soft-delete; filtramos apenas por status.
        IReadOnlyList<Contrato> todos = await contratoRepo.ListAsync(cancellationToken);

        List<Contrato> ativos = todos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToList();

        if (ativos.Count == 0)
        {
            return BuildEnvelope(new BreakdownModalidadeDto([], TotalBrl: 0m));
        }

        // Resolve uma única vez as cotações de todas as moedas estrangeiras presentes.
        IReadOnlySet<Moeda> moedasEstrangeiras = ativos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasPorMoeda =
            await ResolverCotacoesAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Agrupa por modalidade e converte para BRL.
        List<BreakdownModalidadeItemDto> itens = ativos
            .GroupBy(c => c.Modalidade)
            .Select(grupo =>
            {
                decimal valorBrl = CalcularSaldoBrlDoGrupo(grupo, taxasPorMoeda);

                return new BreakdownModalidadeItemDto(
                    Modalidade: grupo.Key.ToString(),
                    QuantidadeContratos: grupo.Count(),
                    // Percentual é calculado após o total; usamos zero como placeholder.
                    ValorBrl: Math.Round(valorBrl, 2, MidpointRounding.AwayFromZero),
                    PercentualTotal: 0m);
            })
            .OrderByDescending(i => i.ValorBrl)
            .ToList();

        decimal totalBrl = Math.Round(
            itens.Sum(i => i.ValorBrl),
            2,
            MidpointRounding.AwayFromZero);

        // Substitui o placeholder com o percentual real.
        // Quando totalBrl == 0 (todos os contratos sem cotação), cada percentual fica zero.
        List<BreakdownModalidadeItemDto> itensComPercentual = itens
            .Select(i => i with
            {
                PercentualTotal = totalBrl == 0m
                    ? 0m
                    : Math.Round(i.ValorBrl / totalBrl * 100m, 4, MidpointRounding.AwayFromZero)
            })
            .ToList();

        BreakdownModalidadeDto data = new(
            Items: itensComPercentual.AsReadOnly(),
            TotalBrl: totalBrl);

        return BuildEnvelope(data);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Soma o saldo BRL de todos os contratos de um grupo de mesma modalidade.
    /// Contratos BRL não sofrem conversão (taxa = 1). Contratos em moeda estrangeira
    /// usam a taxa já resolvida; se ausente, contribuem com zero.
    /// </summary>
    private static decimal CalcularSaldoBrlDoGrupo(
        IGrouping<ModalidadeContrato, Contrato> grupo,
        Dictionary<Moeda, decimal> taxasPorMoeda)
    {
        decimal saldo = 0m;

        foreach (Contrato contrato in grupo)
        {
            decimal taxa = contrato.Moeda == Moeda.Brl
                ? 1m
                : taxasPorMoeda.GetValueOrDefault(contrato.Moeda, defaultValue: 0m);

            saldo = Math.Round(
                saldo + contrato.ValorPrincipal.Valor * taxa,
                6,
                MidpointRounding.AwayFromZero);
        }

        return saldo;
    }

    /// <summary>
    /// Estratégia de cotação idêntica ao <see cref="GetPainelDividaQueryHandler"/> e
    /// ao <see cref="GetSaldoPorBancoAtualQueryHandler"/>:
    /// spot intraday Redis → PTAX D-1 mid-rate → omite (taxa zero).
    /// </summary>
    private async Task<Dictionary<Moeda, decimal>> ResolverCotacoesAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> resultado = new(capacity: moedas.Count);

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = spot.Value.Valor;
                continue;
            }

            CotacaoFx? ptax = await cotacaoResolver.ResolverFxAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                // Mid-rate: (compra + venda) / 2 — arredondamento comercial (HalfUp).
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                resultado[moeda] = midRate;
            }
            // Sem spot e sem PTAX: moeda ausente no dicionário → taxa = 0 na chamada.
        }

        return resultado;
    }

    private EnvelopeResponse<BreakdownModalidadeDto> BuildEnvelope(BreakdownModalidadeDto data) =>
        new(
            Data: data,
            Meta: new EnvelopeMeta(
                DataHoraCalculo: clock.GetCurrentInstant(),
                FontesConsultadas: [],
                Completude: Completude.Completo));
}
