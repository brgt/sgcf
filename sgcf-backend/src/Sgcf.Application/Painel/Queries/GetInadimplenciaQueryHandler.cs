using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Agrega contratos inadimplentes com dias de atraso médio e exposição total em BRL.
///
/// <para>
/// Estratégia de cotação idêntica aos demais handlers do painel:
/// spot intraday Redis → PTAX D-1 mid-rate → zero (moeda sem cotação disponível).
/// </para>
/// <para>
/// Dias de atraso de cada parcela: <c>Ceiling(hoje − DataPrevista)</c> usando
/// <see cref="LocalDate"/> (NodaTime). Nunca usa <c>DateTime.Now</c>.
/// </para>
/// </summary>
public sealed class GetInadimplenciaQueryHandler(
    IContratoRepository contratoRepo,
    IEventoCronogramaRepository cronogramaRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    IResolveTipoCotacaoService cotacaoResolver,
    IClock clock)
    : IRequestHandler<GetInadimplenciaQuery, EnvelopeResponse<InadimplenciaDto>>
{
    // BRT é obrigatório: datas de vencimento estão no calendário brasileiro.
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<InadimplenciaDto>> Handle(
        GetInadimplenciaQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        // Busca apenas os eventos com Status == Atrasado vencidos antes de hoje.
        IReadOnlyList<EventoCronograma> atrasados =
            await cronogramaRepo.ListAtrasadosAntesDeAsync(hoje, cancellationToken);

        if (atrasados.Count == 0)
        {
            return BuildEnvelope(
                new InadimplenciaDto(0, 0, 0m, 0m, []));
        }

        // Resolve cotações para as moedas estrangeiras presentes nos eventos atrasados.
        IReadOnlySet<Moeda> moedasEstrangeiras = atrasados
            .Where(e => e.Moeda != Moeda.Brl)
            .Select(e => e.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasPorMoeda =
            await ResolverCotacoesAsync(moedasEstrangeiras, hoje, cancellationToken);

        // Obtém os contratos referenciados para enriquecer a resposta com NumeroExterno e BancoId.
        HashSet<Guid> contratoIds = atrasados.Select(e => e.ContratoId).ToHashSet();

        IReadOnlyList<Contrato> todosContratos = await contratoRepo.ListAsync(cancellationToken);
        Dictionary<Guid, Contrato> contratosPorId = todosContratos
            .Where(c => contratoIds.Contains(c.Id))
            .ToDictionary(c => c.Id);

        // Resolve apelidos de banco em lote — uma única query para todos os bancos distintos.
        HashSet<Guid> bancoIds = contratosPorId.Values.Select(c => c.BancoId).ToHashSet();
        IReadOnlyList<Banco> bancos = await bancoRepo.ListAllAsync(cancellationToken);
        Dictionary<Guid, string> apelidos = bancos
            .Where(b => bancoIds.Contains(b.Id))
            .ToDictionary(b => b.Id, b => b.Apelido);

        // Agrupa eventos por contrato e calcula métricas por contrato.
        List<InadimplenciaItemDto> itens = atrasados
            .GroupBy(e => e.ContratoId)
            .Select(grupo =>
            {
                Guid contratoId = grupo.Key;
                contratosPorId.TryGetValue(contratoId, out Contrato? contrato);

                string numeroExterno = contrato?.NumeroExterno ?? contratoId.ToString();
                string bancoApelido = contrato is not null
                    ? apelidos.GetValueOrDefault(contrato.BancoId, string.Empty)
                    : string.Empty;

                int diasMaior = 0;
                decimal exposicaoBrl = 0m;

                foreach (EventoCronograma evento in grupo)
                {
                    int dias = DiasDeAtraso(evento.DataPrevista, hoje);
                    if (dias > diasMaior)
                    {
                        diasMaior = dias;
                    }

                    decimal taxa = evento.Moeda == Moeda.Brl
                        ? 1m
                        : taxasPorMoeda.GetValueOrDefault(evento.Moeda, defaultValue: 0m);

                    exposicaoBrl = Math.Round(
                        exposicaoBrl + evento.ValorMoedaOriginal.Valor * taxa,
                        6,
                        MidpointRounding.AwayFromZero);
                }

                return new InadimplenciaItemDto(
                    ContratoId: contratoId,
                    NumeroExterno: numeroExterno,
                    BancoApelido: bancoApelido,
                    QuantidadeParcelasAtrasadas: grupo.Count(),
                    DiasAtrasoMaior: diasMaior,
                    ExposicaoBrl: Math.Round(exposicaoBrl, 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(i => i.ExposicaoBrl)
            .ToList();

        decimal exposicaoTotal = Math.Round(
            itens.Sum(i => i.ExposicaoBrl),
            2,
            MidpointRounding.AwayFromZero);

        // DiasAtrasoMedio é a média dos dias de cada parcela individual (não por contrato).
        // atrasados.Count > 0 aqui — já retornamos zero dto acima quando vazio.
        decimal diasMedio = Math.Round(
            (decimal)atrasados.Sum(e => DiasDeAtraso(e.DataPrevista, hoje)) / atrasados.Count,
            2,
            MidpointRounding.AwayFromZero);

        InadimplenciaDto data = new(
            QuantidadeContratos: itens.Count,
            QuantidadeParcelas: atrasados.Count,
            ExposicaoTotalBrl: exposicaoTotal,
            DiasAtrasoMedio: diasMedio,
            Contratos: itens.AsReadOnly());

        return BuildEnvelope(data);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calcula dias de atraso como o teto de (hoje − dataPrevista).
    /// Um evento vencido há exatamente 1 dia retorna 1; meio-dia retorna 1 também.
    /// Usando subtração de <see cref="LocalDate"/> do NodaTime (retorna <see cref="Period"/>).
    /// </summary>
    private static int DiasDeAtraso(LocalDate dataPrevista, LocalDate hoje)
    {
        // Period.Between retorna dias exatos sem componente fracionário para LocalDate.
        int dias = Period.Between(dataPrevista, hoje, PeriodUnits.Days).Days;

        // Garante que nunca retornamos negativo (proteção defensiva — o filtro na query
        // já garante DataPrevista < hoje, mas datas iguais resultariam em 0 dias).
        return dias < 1 ? 1 : dias;
    }

    /// <summary>
    /// Estratégia de cotação idêntica ao <see cref="GetBreakdownModalidadeQueryHandler"/>:
    /// spot intraday Redis → PTAX D-1 mid-rate → omite (taxa zero na chamada).
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
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                resultado[moeda] = midRate;
            }
            // Sem spot e sem PTAX: moeda ausente → taxa = 0 na chamada.
        }

        return resultado;
    }

    private EnvelopeResponse<InadimplenciaDto> BuildEnvelope(InadimplenciaDto data) =>
        new(
            Data: data,
            Meta: new EnvelopeMeta(
                DataHoraCalculo: clock.GetCurrentInstant(),
                FontesConsultadas: [],
                Completude: Completude.Completo));
}
