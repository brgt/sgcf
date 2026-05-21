using MediatR;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>
/// Retorna a projeção de fluxo de caixa diária para um período informado (default: hoje BRT + 30 dias).
/// Combina eventos previstos do cronograma de contratos com eventos manuais registrados.
/// </summary>
/// <param name="DataDe">Data inicial ISO (yyyy-MM-dd). Quando nula, usa hoje no fuso BRT.</param>
/// <param name="DataAte">Data final ISO (yyyy-MM-dd). Quando nula, usa DataDe + 30 dias.</param>
public sealed record GetFluxoCaixaQuery(string? DataDe, string? DataAte)
    : IRequest<EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>>>;

public sealed class GetFluxoCaixaQueryHandler(
    IEventoCronogramaRepository cronogramaRepo,
    IEventoFluxoCaixaRepository fluxoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetFluxoCaixaQuery, EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>>>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // Limita o período máximo para evitar queries demasiado pesadas.
    private const int MaxDias = 90;
    private const int DefaultDias = 30;

    public async Task<EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>>> Handle(
        GetFluxoCaixaQuery query,
        CancellationToken cancellationToken)
    {
        (LocalDate dataDe, LocalDate dataAte) = ResolverPeriodo(query.DataDe, query.DataAte);

        // Busca as duas fontes em paralelo — sem dependência entre si.
        Task<IReadOnlyList<EventoCronograma>> cronogramaTask =
            cronogramaRepo.ListPrevistosNoPeriodoAsync(dataDe, dataAte, cancellationToken);
        Task<IReadOnlyList<EventoFluxoCaixa>> fluxoTask =
            fluxoRepo.ListByPeriodoAsync(dataDe, dataAte, cancellationToken);

        await Task.WhenAll(cronogramaTask, fluxoTask);

        IReadOnlyList<EventoCronograma> eventosCronograma = cronogramaTask.Result;
        IReadOnlyList<EventoFluxoCaixa> eventosFluxo = fluxoTask.Result;

        // Identifica moedas estrangeiras presentes nos eventos manuais para resolver cotações.
        IReadOnlySet<Moeda> moedasEstrangeiras = eventosFluxo
            .Where(e => e.Valor.Moeda != Moeda.Brl)
            .Select(e => e.Valor.Moeda)
            .ToHashSet();

        (Dictionary<Moeda, decimal> taxas, bool usouFallback)
            = await ResolverTaxasAsync(moedasEstrangeiras, dataDe, cancellationToken);

        // Agrupa por data para acesso O(1) durante a iteração de dias.
        ILookup<LocalDate, EventoCronograma> cronogramaPorDia =
            eventosCronograma.ToLookup(e => e.DataPrevista);
        ILookup<LocalDate, EventoFluxoCaixa> fluxoPorDia =
            eventosFluxo.ToLookup(e => e.Data);

        List<FluxoCaixaDiaDto> dias = new(Period.Between(dataDe, dataAte, PeriodUnits.Days).Days + 1);
        decimal saldoAcumulado = 0m;

        for (LocalDate dia = dataDe; dia <= dataAte; dia = dia.PlusDays(1))
        {
            List<FluxoCaixaEventoDto> eventos = new();

            // Eventos do cronograma — todos são Saida (a empresa é devedora).
            foreach (EventoCronograma ec in cronogramaPorDia[dia])
            {
                decimal valorBrl = ResolverValorBrl(ec);
                eventos.Add(new FluxoCaixaEventoDto(
                    Origem: "cronograma",
                    Tipo: "Saida",
                    Descricao: $"Contrato {ec.ContratoId} — {ec.Tipo}",
                    ValorBrl: Math.Round(valorBrl, 2, MidpointRounding.AwayFromZero)));
            }

            // Eventos manuais.
            foreach (EventoFluxoCaixa ef in fluxoPorDia[dia])
            {
                decimal valorBrl = ef.Valor.Moeda == Moeda.Brl
                    ? ef.Valor.Valor
                    : taxas.TryGetValue(ef.Valor.Moeda, out decimal taxa)
                        ? Math.Round(ef.Valor.Valor * taxa, 6, MidpointRounding.AwayFromZero)
                        : 0m;

                eventos.Add(new FluxoCaixaEventoDto(
                    Origem: "manual",
                    Tipo: ef.Tipo.ToString(),
                    Descricao: ef.Descricao,
                    ValorBrl: Math.Round(valorBrl, 2, MidpointRounding.AwayFromZero)));
            }

            decimal entradasBrl = eventos
                .Where(e => e.Tipo == "Entrada")
                .Sum(e => e.ValorBrl);

            decimal saidasBrl = eventos
                .Where(e => e.Tipo == "Saida")
                .Sum(e => e.ValorBrl);

            saldoAcumulado = Math.Round(
                saldoAcumulado + entradasBrl - saidasBrl,
                2,
                MidpointRounding.AwayFromZero);

            List<string> alertas = [];
            if (saldoAcumulado < 0m)
            {
                alertas.Add($"Saldo projetado negativo em {dia:yyyy-MM-dd}");
            }

            dias.Add(new FluxoCaixaDiaDto(
                Data: dia.ToString("yyyy-MM-dd", null),
                EntradasBrl: Math.Round(entradasBrl, 2, MidpointRounding.AwayFromZero),
                SaidasBrl: Math.Round(saidasBrl, 2, MidpointRounding.AwayFromZero),
                SaldoProjetadoBrl: saldoAcumulado,
                Eventos: eventos.AsReadOnly(),
                Alertas: alertas.AsReadOnly()));
        }

        IReadOnlyList<FonteConsultada> fontes =
        [
            new FonteConsultada("cronograma", "ok", eventosCronograma.Count),
            new FonteConsultada("eventos_fluxo", "ok", eventosFluxo.Count)
        ];

        Completude completude = usouFallback ? Completude.Parcial : Completude.Completo;

        EnvelopeMeta meta = new(
            DataHoraCalculo: clock.GetCurrentInstant(),
            FontesConsultadas: fontes,
            Completude: completude);

        return new EnvelopeResponse<IReadOnlyList<FluxoCaixaDiaDto>>(dias.AsReadOnly(), meta);
    }

    /// <summary>
    /// Resolve o valor em BRL de um evento de cronograma.
    /// Usa <c>ValorBrlEstimado</c> quando disponível; caso contrário, usa <c>ValorMoedaOriginal</c>
    /// sem conversão (já em BRL para contratos domésticos; 0 para moeda estrangeira sem cotação).
    /// </summary>
    private static decimal ResolverValorBrl(EventoCronograma ec)
    {
        if (ec.ValorBrlEstimado.HasValue)
        {
            return ec.ValorBrlEstimado.Value.Valor;
        }

        // Eventos em BRL não precisam de conversão.
        if (ec.Moeda == Moeda.Brl)
        {
            return ec.ValorMoedaOriginal.Valor;
        }

        // Moeda estrangeira sem estimativa BRL: usa zero → Completude.Parcial.
        return 0m;
    }

    private (LocalDate dataDe, LocalDate dataAte) ResolverPeriodo(string? paramDe, string? paramAte)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        LocalDate dataDe = string.IsNullOrWhiteSpace(paramDe)
            ? hoje
            : IsoPattern.Parse(paramDe) is { Success: true } r ? r.Value : hoje;

        LocalDate dataAte = string.IsNullOrWhiteSpace(paramAte)
            ? dataDe.PlusDays(DefaultDias)
            : IsoPattern.Parse(paramAte) is { Success: true } r2 ? r2.Value : dataDe.PlusDays(DefaultDias);

        // Garante que dataAte nunca é anterior a dataDe.
        if (dataAte < dataDe)
        {
            dataAte = dataDe.PlusDays(DefaultDias);
        }

        // Clamp ao máximo de 90 dias para proteger a performance da query.
        int totalDias = Period.Between(dataDe, dataAte, PeriodUnits.Days).Days;
        if (totalDias > MaxDias)
        {
            dataAte = dataDe.PlusDays(MaxDias);
        }

        return (dataDe, dataAte);
    }

    private async Task<(Dictionary<Moeda, decimal> taxas, bool usouFallback)> ResolverTaxasAsync(
        IReadOnlySet<Moeda> moedas,
        LocalDate dataRef,
        CancellationToken cancellationToken)
    {
        Dictionary<Moeda, decimal> resultado = new();
        bool usouFallback = false;

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = spot.Value.Valor;
            }
            else
            {
                CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                    moeda, TipoCotacao.PtaxD1, dataRef, cancellationToken);

                if (ptax is not null)
                {
                    decimal midRate = Math.Round(
                        (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                        6,
                        MidpointRounding.AwayFromZero);
                    resultado[moeda] = midRate;
                    usouFallback = true;
                }
                else
                {
                    // Sem cotação: usa zero — Completude.Parcial sinaliza ao cliente.
                    resultado[moeda] = 0m;
                    usouFallback = true;
                }
            }
        }

        return (resultado, usouFallback);
    }
}
