using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Common;
using Sgcf.Application.Contabilidade;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Painel;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta a estrutura de capital consolidada com ICR (Índice de Cobertura de Receitas).
/// ICR = EBITDA dos últimos 12 meses / Despesa Financeira dos últimos 12 meses.
/// Quando a Despesa Financeira é zero, ICR retorna 0 (sem divisão por zero).
/// Quando dados contábeis estão ausentes, a completude é Parcial e um alerta é incluído.
/// </summary>
public sealed class GetEstruturaCapitalQueryHandler(
    IMediator mediator,
    IEbitdaMensalRepository ebitdaRepo,
    IDadosContabeisRepository contabeisRepo,
    IClock clock)
    : IRequestHandler<GetEstruturaCapitalQuery, EnvelopeResponse<EstruturaCapitalDto>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<EstruturaCapitalDto>> Handle(
        GetEstruturaCapitalQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        // Dívida total via painel existente (sem filtros — visão consolidada).
        PainelDividaDto painel = await mediator.Send(new GetPainelDividaQuery(), cancellationToken);
        decimal dividaTotalBrl = painel.DividaBrutaBrl;

        // EBITDA e dados contábeis dos últimos 12 meses.
        IReadOnlyList<EbitdaMensal> ebitdas = await ebitdaRepo.ListUltimos12MesesAsync(
            hoje.Year, hoje.Month, cancellationToken);

        IReadOnlyList<DadosContabeisMensal> contabeis = await contabeisRepo.ListUltimos12MesesAsync(
            hoje.Year, hoje.Month, cancellationToken);

        decimal ebitda12m = Math.Round(
            ebitdas.Sum(e => e.ValorBrl.Valor),
            2,
            MidpointRounding.AwayFromZero);

        decimal despesaFinanceira12m = Math.Round(
            contabeis.Sum(c => c.DespesaFinanceira.Valor),
            2,
            MidpointRounding.AwayFromZero);

        // PL: usa o mês mais recente disponível como proxy de saldo atual.
        // Se não houver dado, retorna zero e marca como Parcial.
        decimal plBrl = contabeis.Count > 0
            ? Math.Round(
                contabeis
                    .OrderByDescending(c => c.Ano * 100 + c.Mes)
                    .First()
                    .PatrimonioLiquido.Valor,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        bool dadosContabeisAusentes = contabeis.Count == 0;

        List<string> alertas = new();
        if (dadosContabeisAusentes)
        {
            alertas.Add("DADOS_CONTABEIS_AUSENTES");
        }

        Completude completude = dadosContabeisAusentes ? Completude.Parcial : Completude.Completo;

        decimal dividaSobrePatrimonio = plBrl != 0m
            ? Math.Round(dividaTotalBrl / plBrl, 6, MidpointRounding.AwayFromZero)
            : 0m;

        // ICR = 0 quando despesa financeira é zero — divisão por zero não ocorre.
        decimal icr = despesaFinanceira12m != 0m
            ? Math.Round(ebitda12m / despesaFinanceira12m, 6, MidpointRounding.AwayFromZero)
            : 0m;

        EstruturaCapitalDto dto = new(
            DividaTotalBrl: dividaTotalBrl,
            PatrimonioLiquidoBrl: plBrl,
            DividaSobrePatrimonio: dividaSobrePatrimonio,
            EbitdaUltimos12mBrl: ebitda12m,
            DespesaFinanceira12mBrl: despesaFinanceira12m,
            Icr: icr,
            Alertas: alertas.AsReadOnly(),
            Completude: completude);

        EnvelopeMeta meta = new(
            DataHoraCalculo: agora,
            FontesConsultadas:
            [
                new FonteConsultada("contratos", "ok", painel.BreakdownPorMoeda.Sum(b => b.QuantidadeContratos)),
                new FonteConsultada("ebitda_mensal", "ok", ebitdas.Count),
                new FonteConsultada("dados_contabeis_mensal", dadosContabeisAusentes ? "ausente" : "ok", contabeis.Count)
            ],
            Completude: completude);

        return new EnvelopeResponse<EstruturaCapitalDto>(dto, meta);
    }
}
