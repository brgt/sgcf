using MediatR;
using NodaTime;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Ranqueia contratos ativos por economia líquida de antecipação, considerando o custo de oportunidade CDI.
/// Exclui automaticamente contratos com Padrão B (Sicredi) — cobra juros totais, nunca gera economia.
/// </summary>
public sealed class SimularAntecipacaoPortfolioQueryHandler(
    IContratoRepository contratoRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<SimularAntecipacaoPortfolioQuery, ResultadoAntecipacaoPortfolioDto>
{
    private const string MotivoExclusaoPadraoB =
        "Sicredi cobra juros totais no período — antecipação não gera economia";

    private const int TopN = 5;

    public async Task<ResultadoAntecipacaoPortfolioDto> Handle(
        SimularAntecipacaoPortfolioQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);
        IReadOnlyList<Banco> bancos = await bancoRepo.ListAllAsync(cancellationToken);

        IReadOnlyList<Contrato> contratosAtivos = contratos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToList()
            .AsReadOnly();

        Dictionary<Guid, Banco> bancosPorId = bancos.ToDictionary(b => b.Id);

        // Resolve cotações para conversão de saldo
        IReadOnlySet<Moeda> moedasEstrangeiras = contratosAtivos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasConversao =
            await ResolverTaxasAsync(moedasEstrangeiras, hoje, cancellationToken);

        List<RecomendacaoAntecipacaoDto> recomendacoes = new();
        List<string> contratosExcluidos = new();

        foreach (Contrato contrato in contratosAtivos)
        {
            if (!bancosPorId.TryGetValue(contrato.BancoId, out Banco? banco))
            {
                contratosExcluidos.Add(
                    $"{contrato.NumeroExterno}: banco {contrato.BancoId} não encontrado");
                continue;
            }

            // Exclui Padrão B — cobra juros totais, nunca gera economia
            if (banco.PadraoAntecipacao == PadraoAntecipacao.B)
            {
                contratosExcluidos.Add($"{contrato.NumeroExterno}: {MotivoExclusaoPadraoB}");
                continue;
            }

            // Converte principal para BRL
            decimal taxa = contrato.Moeda == Moeda.Brl
                ? 1m
                : taxasConversao.TryGetValue(contrato.Moeda, out decimal t) ? t : 0m;

            decimal principalBrl = Math.Round(
                contrato.ValorPrincipal.Valor * taxa,
                6,
                MidpointRounding.AwayFromZero);

            // Verifica disponibilidade de caixa
            if (principalBrl > query.CaixaDisponivelBrl)
            {
                contratosExcluidos.Add(
                    $"{contrato.NumeroExterno}: caixa insuficiente ({principalBrl:N2} BRL necessário)");
                continue;
            }

            EntradaSimulacaoAntecipacao entrada = MontarEntradaParaHoje(contrato, hoje);

            // Valida restrições do banco
            (bool restricoesPermitem, IReadOnlyList<string> alertasRestricao) =
                AntecipacaoValidador.Validar(banco, entrada, hoje, hoje);

            ResultadoSimulacaoAntecipacao resultado =
                AntecipacaoStrategyDispatcher.Calcular(banco.PadraoAntecipacao, entrada, banco);

            bool permitido = restricoesPermitem && resultado.Permitido;

            List<string> restricoes = new(alertasRestricao);
            if (!resultado.Permitido)
            {
                restricoes.AddRange(resultado.Alertas.Where(a => a.StartsWith("RESTRIÇÃO", StringComparison.Ordinal)));
            }

            if (!permitido && restricoes.Count > 0)
            {
                // Inclui na lista de excluídos apenas se completamente bloqueado
                contratosExcluidos.Add(
                    $"{contrato.NumeroExterno}: bloqueado por restrições bancárias");
                continue;
            }

            decimal totalAntecipacaoBrl = Math.Round(
                resultado.TotalAQuitar.Valor * taxa,
                2,
                MidpointRounding.AwayFromZero);

            // Custo de oportunidade CDI = caixa_usado × taxa_cdi × prazo_remanescente / 365
            decimal custoOportunidadeCdi = 0m;
            if (query.TaxaCdiAa.HasValue && query.TaxaCdiAa.Value > 0m)
            {
                decimal taxaCdiFracao = query.TaxaCdiAa.Value / 100m;
                custoOportunidadeCdi = Math.Round(
                    principalBrl * taxaCdiFracao * entrada.PrazoRemanescenteDias / 365m,
                    6,
                    MidpointRounding.AwayFromZero);
            }

            // Custo se não antecipar: principal + juros restantes (juros simples)
            decimal jurosRestantes = Math.Round(
                contrato.ValorPrincipal.Valor * contrato.TaxaAa.AsDecimal
                    * entrada.PrazoRemanescenteDias / (decimal)contrato.BaseCalculo,
                6,
                MidpointRounding.AwayFromZero);

            decimal custoSeNaoAnteciparBrl = Math.Round(
                (contrato.ValorPrincipal.Valor + jurosRestantes) * taxa,
                6,
                MidpointRounding.AwayFromZero);

            decimal economiaLiquida = Math.Round(
                custoSeNaoAnteciparBrl - totalAntecipacaoBrl - custoOportunidadeCdi,
                2,
                MidpointRounding.AwayFromZero);

            string justificativa = economiaLiquida > 0m
                ? $"Antecipar economiza {economiaLiquida:N2} BRL após custo de oportunidade CDI de {custoOportunidadeCdi:N2} BRL"
                : $"Antecipar não gera economia com CDI a {query.TaxaCdiAa ?? 0m:N1}% a.a.";

            recomendacoes.Add(new RecomendacaoAntecipacaoDto(
                ContratoId: contrato.Id,
                NumeroExterno: contrato.NumeroExterno,
                Banco: banco.Apelido,
                Modalidade: contrato.Modalidade.ToString(),
                EconomiaLiquidaBrl: economiaLiquida,
                CustoPrepagamentoBrl: totalAntecipacaoBrl,
                ValorTotalAntecipacaoBrl: principalBrl,
                JustificativaOtimizacao: justificativa,
                Restricoes: restricoes.AsReadOnly()));
        }

        IReadOnlyList<RecomendacaoAntecipacaoDto> rankingTop5 = recomendacoes
            .OrderByDescending(r => r.EconomiaLiquidaBrl)
            .Take(TopN)
            .ToList()
            .AsReadOnly();

        return new ResultadoAntecipacaoPortfolioDto(
            CaixaDisponivelBrl: query.CaixaDisponivelBrl,
            RankingTop5: rankingTop5,
            ContratosExcluidos: contratosExcluidos.AsReadOnly());
    }

    /// <summary>
    /// Monta a entrada de simulação assumindo liquidação total hoje.
    /// Parâmetros calculados deterministicamente com base nos dados do contrato.
    /// </summary>
    private static EntradaSimulacaoAntecipacao MontarEntradaParaHoje(Contrato contrato, LocalDate hoje)
    {
        int prazoTotalDias = Period.Between(
            contrato.DataContratacao, contrato.DataVencimento, PeriodUnits.Days).Days;

        int prazoRemanescenteDias = Math.Max(
            0,
            Period.Between(hoje, contrato.DataVencimento, PeriodUnits.Days).Days);

        int prazoRemanescenteMeses = (int)Math.Ceiling(prazoRemanescenteDias / 30.0);

        int diasAcumulados = Math.Max(
            0,
            Period.Between(contrato.DataContratacao, hoje, PeriodUnits.Days).Days);

        decimal jurosProRataDecimal = Math.Round(
            contrato.ValorPrincipal.Valor * contrato.TaxaAa.AsDecimal
                * diasAcumulados / (decimal)contrato.BaseCalculo,
            6,
            MidpointRounding.AwayFromZero);

        Money? jurosProRata = jurosProRataDecimal > 0m
            ? new Money(jurosProRataDecimal, contrato.Moeda)
            : (Money?)null;

        return new EntradaSimulacaoAntecipacao(
            Tipo: TipoAntecipacao.LiquidacaoTotalAntecipada,
            PrincipalAQuitar: contrato.ValorPrincipal,
            JurosProRata: jurosProRata,
            PrazoTotalOriginalDias: prazoTotalDias,
            PrazoRemanescenteDias: prazoRemanescenteDias,
            PrazoRemanescenteMeses: prazoRemanescenteMeses,
            TaxaAa: contrato.TaxaAa,
            BaseCalculo: (int)contrato.BaseCalculo,
            TaxaMercadoAtualAa: null,
            IndenizacaoBanco: null,
            OrigemRefinanciamentoInterno: false);
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
