using System.Collections.ObjectModel;
using MediatR;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>
/// Retorna a posição consolidada de caixa em BRL para a data informada (ou hoje BRT).
/// Estratégia de cotação: spot intraday → PTAX D-1 → zero + Completude.Parcial.
/// </summary>
/// <param name="DataReferencia">
/// Data de referência ISO (yyyy-MM-dd). Quando nula, usa o dia corrente no fuso BRT.
/// </param>
public sealed record GetPosicaoCaixaQuery(string? DataReferencia)
    : IRequest<EnvelopeResponse<PosicaoCaixaDto>>;

public sealed class GetPosicaoCaixaQueryHandler(
    ISaldoCaixaRepository saldoRepo,
    IContaBancariaRepository contaRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetPosicaoCaixaQuery, EnvelopeResponse<PosicaoCaixaDto>>
{
    private static readonly LocalDatePattern IsoPattern = LocalDatePattern.Iso;
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<PosicaoCaixaDto>> Handle(
        GetPosicaoCaixaQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate dataRef = ResolverDataReferencia(query.DataReferencia);

        // Carrega contas e saldos em paralelo.
        Task<IReadOnlyList<ContaBancaria>> contasTask = contaRepo.ListAsync(
            apenasAtivas: null, cancellationToken);
        Task<IReadOnlyList<SaldoCaixa>> saldosTask = saldoRepo.ListByDataAsync(
            dataRef, cancellationToken);

        await Task.WhenAll(contasTask, saldosTask);

        IReadOnlyList<ContaBancaria> todasContas = contasTask.Result;
        IReadOnlyList<SaldoCaixa> saldos = saldosTask.Result;

        // Indexa saldos por ContaId para lookup O(1).
        Dictionary<Guid, SaldoCaixa> saldoPorConta = saldos
            .ToDictionary(s => s.ContaId);

        // Identifica moedas estrangeiras para resolver cotações.
        IReadOnlySet<Moeda> moedasEstrangeiras = todasContas
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        (Dictionary<Moeda, (decimal taxa, bool ehSpot)> taxas, bool algumFallback)
            = await ResolverTaxasAsync(moedasEstrangeiras, dataRef, cancellationToken);

        // Verifica se alguma conta ativa não tem saldo na data de referência.
        bool saldoFaltando = todasContas
            .Where(c => c.Ativa)
            .Any(c => !saldoPorConta.ContainsKey(c.Id));

        bool completudeParcial = algumFallback || saldoFaltando;

        // Monta linhas por conta com conversão para BRL.
        List<PosicaoCaixaContaDto> linhasContas = new(todasContas.Count);

        foreach (ContaBancaria conta in todasContas)
        {
            decimal saldoMoeda = saldoPorConta.TryGetValue(conta.Id, out SaldoCaixa? s)
                ? s.Valor.Valor
                : 0m;

            decimal taxa = conta.Moeda == Moeda.Brl
                ? 1m
                : taxas.TryGetValue(conta.Moeda, out (decimal taxa, bool ehSpot) cotacao)
                    ? cotacao.taxa
                    : 0m;

            decimal saldoBrl = Math.Round(saldoMoeda * taxa, 6, MidpointRounding.AwayFromZero);

            linhasContas.Add(new PosicaoCaixaContaDto(
                ContaId: conta.Id,
                NomeConta: conta.Nome,
                Agencia: conta.Agencia,
                NumeroConta: conta.NumeroConta,
                Moeda: conta.Moeda.ToString().ToUpperInvariant(),
                Saldo: Math.Round(saldoMoeda, 6, MidpointRounding.AwayFromZero),
                SaldoBrl: Math.Round(saldoBrl, 2, MidpointRounding.AwayFromZero)));
        }

        // Agrega por moeda.
        IReadOnlyList<PosicaoCaixaMoedaDto> porMoeda = AgregarPorMoeda(linhasContas, todasContas, taxas);

        // Agrega por banco usando o BancoId da conta; NomeBanco = "Banco {BancoId}" quando
        // IBancoRepository não existe nesta camada (princípio de não criar abstrações desnecessárias).
        IReadOnlyList<PosicaoCaixaBancoDto> porBanco = AgregarPorBanco(linhasContas, todasContas);

        decimal saldoConsolidado = Math.Round(
            linhasContas.Sum(c => c.SaldoBrl),
            2,
            MidpointRounding.AwayFromZero);

        PosicaoCaixaDto data = new(
            DataReferencia: dataRef.ToString("yyyy-MM-dd", null),
            SaldoConsolidadoBrl: saldoConsolidado,
            PorMoeda: porMoeda,
            PorBanco: porBanco);

        IReadOnlyList<FonteConsultada> fontes = DeterminarFontes(taxas, moedasEstrangeiras);

        EnvelopeMeta meta = new(
            DataHoraCalculo: clock.GetCurrentInstant(),
            FontesConsultadas: fontes,
            Completude: completudeParcial ? Completude.Parcial : Completude.Completo);

        return new EnvelopeResponse<PosicaoCaixaDto>(data, meta);
    }

    private LocalDate ResolverDataReferencia(string? dataParam)
    {
        if (string.IsNullOrWhiteSpace(dataParam))
        {
            return clock.GetCurrentInstant().InZone(FusoBrasilia).Date;
        }

        ParseResult<LocalDate> parseResult = IsoPattern.Parse(dataParam);
        return parseResult.Success
            ? parseResult.Value
            : clock.GetCurrentInstant().InZone(FusoBrasilia).Date;
    }

    private async Task<(Dictionary<Moeda, (decimal taxa, bool ehSpot)>, bool algumFallback)>
        ResolverTaxasAsync(
            IReadOnlySet<Moeda> moedas,
            LocalDate dataRef,
            CancellationToken cancellationToken)
    {
        Dictionary<Moeda, (decimal taxa, bool ehSpot)> resultado = new();
        bool algumFallback = false;

        foreach (Moeda moeda in moedas)
        {
            Money? spot = await spotCache.GetSpotAsync(moeda, cancellationToken);

            if (spot is not null)
            {
                resultado[moeda] = (spot.Value.Valor, true);
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
                    resultado[moeda] = (midRate, false);
                    algumFallback = true;
                }
                else
                {
                    // Sem cotação disponível: taxa zero → Completude.Parcial.
                    resultado[moeda] = (0m, false);
                    algumFallback = true;
                }
            }
        }

        return (resultado, algumFallback);
    }

    private static ReadOnlyCollection<PosicaoCaixaMoedaDto> AgregarPorMoeda(
        IReadOnlyList<PosicaoCaixaContaDto> linhas,
        IReadOnlyList<ContaBancaria> contas,
        Dictionary<Moeda, (decimal taxa, bool ehSpot)> taxas)
    {
        // Monta dicionário de moeda da conta pelo ContaId.
        Dictionary<Guid, Moeda> moedaPorConta = contas.ToDictionary(c => c.Id, c => c.Moeda);

        return linhas
            .GroupBy(l => moedaPorConta.TryGetValue(l.ContaId, out Moeda m) ? m : Moeda.Brl)
            .Select(g =>
            {
                Moeda moeda = g.Key;
                decimal saldoBrl = Math.Round(g.Sum(l => l.SaldoBrl), 2, MidpointRounding.AwayFromZero);
                decimal saldoOriginal = Math.Round(g.Sum(l => l.Saldo), 6, MidpointRounding.AwayFromZero);
                decimal taxa = moeda == Moeda.Brl
                    ? 1m
                    : taxas.TryGetValue(moeda, out (decimal taxa, bool ehSpot) c) ? c.taxa : 0m;

                return new PosicaoCaixaMoedaDto(
                    Moeda: moeda.ToString().ToUpperInvariant(),
                    SaldoBrl: saldoBrl,
                    SaldoMoedaOriginal: saldoOriginal,
                    Taxa: Math.Round(taxa, 6, MidpointRounding.AwayFromZero));
            })
            .ToList()
            .AsReadOnly();
    }

    private static ReadOnlyCollection<PosicaoCaixaBancoDto> AgregarPorBanco(
        IReadOnlyList<PosicaoCaixaContaDto> linhas,
        IReadOnlyList<ContaBancaria> contas)
    {
        // Indexa contas por Id para cross-reference com linhas de conta.
        Dictionary<Guid, ContaBancaria> contaPorId = contas.ToDictionary(c => c.Id);

        return linhas
            .GroupBy(l => contaPorId.TryGetValue(l.ContaId, out ContaBancaria? c) ? c.BancoId : Guid.Empty)
            .Select(g =>
            {
                Guid bancoId = g.Key;

                // Sem IBancoRepository nesta camada: usa "Banco {BancoId}" como placeholder.
                // O frontend pode enriquecer com o nome real via endpoint /api/v1/bancos/{id}.
                string nomeBanco = $"Banco {bancoId}";

                decimal saldoBanco = Math.Round(g.Sum(l => l.SaldoBrl), 2, MidpointRounding.AwayFromZero);

                return new PosicaoCaixaBancoDto(
                    BancoId: bancoId,
                    NomeBanco: nomeBanco,
                    SaldoBrl: saldoBanco,
                    Contas: g.ToList().AsReadOnly());
            })
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<FonteConsultada> DeterminarFontes(
        Dictionary<Moeda, (decimal taxa, bool ehSpot)> taxas,
        IReadOnlySet<Moeda> moedasEstrangeiras)
    {
        if (moedasEstrangeiras.Count == 0 || !taxas.Values.Any(v => v.taxa > 0m))
        {
            return [];
        }

        bool todosSpot = taxas.Values.All(v => v.ehSpot);
        string fonte = todosSpot ? "cotacao_spot_intraday" : "ptax_d1";

        return [new FonteConsultada(fonte, "ok", null)];
    }
}
