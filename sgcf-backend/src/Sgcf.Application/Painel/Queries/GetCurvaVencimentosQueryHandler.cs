using System.Collections.Frozen;
using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta a curva de vencimentos futuros para o horizonte solicitado,
/// agrupando eventos em buckets temporais (mês / trimestre / ano)
/// com breakdown por modalidade de contrato. Converte todos os valores
/// para BRL via spot intraday ou PTAX D-1, reutilizando a mesma estratégia
/// de cotação empregada pelo painel de dívida.
/// </summary>
public sealed class GetCurvaVencimentosQueryHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock)
    : IRequestHandler<GetCurvaVencimentosQuery, EnvelopeResponse<CurvaVencimentosDto>>
{
    // Horizontes suportados; qualquer valor fora desse conjunto usa 12 como fallback.
    // FrozenSet: O(1) lookup, alocado uma vez, ideal para conjunto estático pequeno.
    private static readonly FrozenSet<int> HorizontesValidos = new HashSet<int> { 12, 24, 36, 60 }.ToFrozenSet();

    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<CurvaVencimentosDto>> Handle(
        GetCurvaVencimentosQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        int mesesEfetivos = HorizontesValidos.Contains(query.Meses) ? query.Meses : 12;

        // dataFim é o último dia do mês que completa o horizonte.
        // Ex: hoje = 2026-05-21, meses = 12 → dataFim = 2027-05-31
        LocalDate dataFim = hoje.PlusMonths(mesesEfetivos);

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        contratos = AplicarFiltros(contratos, query);

        if (contratos.Count == 0)
        {
            return ConstruirEnvelope(new CurvaVencimentosDto([], 0m));
        }

        IReadOnlyList<Guid> contratoIds = contratos.Select(c => c.Id).ToList().AsReadOnly();

        // Resolve cotações para as moedas estrangeiras presentes nos contratos filtrados.
        IReadOnlySet<Moeda> moedasEstrangeiras = contratos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasConversao =
            await ResolverTaxasAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Mapas para lookups O(1) durante a construção dos buckets.
        Dictionary<Guid, Moeda> moedaPorContrato = contratos.ToDictionary(c => c.Id, c => c.Moeda);
        Dictionary<Guid, string> modalidadePorContrato =
            contratos.ToDictionary(c => c.Id, c => c.Modalidade.ToString());

        IReadOnlyList<EventoCronograma> eventos =
            await cronogramaRepo.ListAbertosNoPeriodoAsync(hoje, dataFim, contratoIds, cancellationToken);

        // Acumula o valor BRL de cada evento, agrupado por (bucketLabel, modalidade).
        var acumulado = new Dictionary<(string Label, string Modalidade), decimal>();

        foreach (EventoCronograma evento in eventos)
        {
            // Apenas Principal e Juros compõem o fluxo de saída.
            if (evento.Tipo != TipoEventoCronograma.Principal
                && evento.Tipo != TipoEventoCronograma.Juros)
            {
                continue;
            }

            Moeda moeda = moedaPorContrato.TryGetValue(evento.ContratoId, out Moeda m) ? m : evento.Moeda;
            decimal taxa = moeda == Moeda.Brl
                ? 1m
                : taxasConversao.TryGetValue(moeda, out decimal t) ? t : 0m;

            decimal valorBrl = Math.Round(
                evento.ValorMoedaOriginal.Valor * taxa,
                6,
                MidpointRounding.AwayFromZero);

            string label = CalcularLabel(evento.DataPrevista, query.Granularidade);
            string modalidade = modalidadePorContrato.TryGetValue(evento.ContratoId, out string? mod)
                ? mod
                : "Desconhecida";

            var chave = (label, modalidade);
            acumulado[chave] = acumulado.TryGetValue(chave, out decimal existente)
                ? Math.Round(existente + valorBrl, 6, MidpointRounding.AwayFromZero)
                : valorBrl;
        }

        // Constrói os buckets ordenados cronologicamente com breakdown por modalidade.
        List<BucketVencimentoDto> buckets = acumulado
            .GroupBy(kv => kv.Key.Label)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                IReadOnlyList<BucketModalidadeDto> porModalidade = g
                    .OrderBy(kv => kv.Key.Modalidade)
                    .Select(kv => new BucketModalidadeDto(
                        kv.Key.Modalidade,
                        Math.Round(kv.Value, 2, MidpointRounding.AwayFromZero)))
                    .ToList()
                    .AsReadOnly();

                decimal totalBucket = Math.Round(
                    porModalidade.Sum(p => p.ValorBrl),
                    2,
                    MidpointRounding.AwayFromZero);

                return new BucketVencimentoDto(g.Key, totalBucket, porModalidade);
            })
            .ToList();

        decimal totalBrl = Math.Round(
            buckets.Sum(b => b.TotalBrl),
            2,
            MidpointRounding.AwayFromZero);

        return ConstruirEnvelope(new CurvaVencimentosDto(buckets.AsReadOnly(), totalBrl));
    }

    /// <summary>
    /// Calcula o label do bucket conforme a granularidade:
    /// <c>YYYY-MM</c>, <c>YYYY-Qx</c> ou <c>YYYY</c>.
    /// </summary>
    private static string CalcularLabel(LocalDate data, GranularidadeHorizonte granularidade)
    {
        return granularidade switch
        {
            GranularidadeHorizonte.Mes => $"{data.Year:D4}-{data.Month:D2}",
            GranularidadeHorizonte.Trimestre => $"{data.Year:D4}-Q{TrimestreDeMes(data.Month)}",
            GranularidadeHorizonte.Ano => $"{data.Year:D4}",
            _ => $"{data.Year:D4}-{data.Month:D2}"
        };
    }

    /// <summary>
    /// Converte o número do mês (1-12) para o número do trimestre (1-4).
    /// Q1 = Jan-Mar, Q2 = Abr-Jun, Q3 = Jul-Set, Q4 = Out-Dez.
    /// </summary>
    private static int TrimestreDeMes(int mes) => (mes - 1) / 3 + 1;

    private static System.Collections.ObjectModel.ReadOnlyCollection<Contrato> AplicarFiltros(
        IReadOnlyList<Contrato> contratos,
        GetCurvaVencimentosQuery query)
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

    /// <summary>
    /// Resolve a taxa de conversão BRL para cada moeda estrangeira.
    /// Estratégia: spot intraday (Redis) → PTAX D-1 (banco de dados) → ausente (zero).
    /// Idêntica à estratégia usada em <c>GetCalendarioVencimentosQueryHandler</c>.
    /// </summary>
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

            CotacaoFx? ptax = await cotacaoResolver.ResolverFxAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                resultado[moeda] = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
            }
        }

        return resultado;
    }

    private EnvelopeResponse<CurvaVencimentosDto> ConstruirEnvelope(CurvaVencimentosDto data)
    {
        // O EnvelopeResultFilter do controlador também poderia envolver a resposta,
        // mas o handler retorna o envelope diretamente para poder inserir fontes consultadas
        // em versões futuras sem alterar a assinatura da query.
        EnvelopeMeta meta = new(
            DataHoraCalculo: clock.GetCurrentInstant(),
            FontesConsultadas: [new FonteConsultada("banco_de_dados", "ok", null)],
            Completude: Completude.Completo);

        return new EnvelopeResponse<CurvaVencimentosDto>(data, meta);
    }
}
