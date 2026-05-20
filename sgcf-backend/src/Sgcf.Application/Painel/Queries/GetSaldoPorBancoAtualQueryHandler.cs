using MediatR;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Monta o saldo atual da carteira de contratos agrupado por banco em BRL.
/// Reutiliza exatamente a mesma estratégia de resolução de cotações do
/// <see cref="GetPainelDividaQueryHandler"/>: spot intraday via Redis,
/// fallback para PTAX D-1 com mid-rate (compra+venda)/2.
/// Isso garante que Σ SaldoPorBanco == PainelDivida.DividaBrutaBrl
/// quando não há filtros.
/// </summary>
public sealed class GetSaldoPorBancoAtualQueryHandler(
    IContratoRepository contratoRepo,
    IBancoRepository bancoRepo,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoFxRepo,
    IClock clock)
    : IRequestHandler<GetSaldoPorBancoAtualQuery, SaldoPorBancoAtualDto>
{
    public async Task<SaldoPorBancoAtualDto> Handle(
        GetSaldoPorBancoAtualQuery query,
        CancellationToken cancellationToken)
    {
        // Datas de calendário brasileiro derivam do fuso BRT, não UTC.
        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        // 1. Carrega todos os contratos não-deletados (query filter EF aplica soft-delete)
        IReadOnlyList<Contrato> todos = await contratoRepo.ListAsync(cancellationToken);

        // 2. Filtra apenas os ativos — mesmo padrão do GetPainelDividaQueryHandler
        List<Contrato> ativos = todos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToList();

        if (ativos.Count == 0)
        {
            return new SaldoPorBancoAtualDto(
                Bancos: [],
                SaldoTotalBrl: 0m,
                DataReferencia: hoje);
        }

        // 3. Resolve cotações para todas as moedas estrangeiras presentes
        IReadOnlySet<Moeda> moedasEstrangeiras = ativos
            .Where(c => c.Moeda != Moeda.Brl)
            .Select(c => c.Moeda)
            .ToHashSet();

        Dictionary<Moeda, decimal> taxasPorMoeda =
            await ResolverCotacoesAsync(moedasEstrangeiras, hoje, cancellationToken);

        // 4. Agrupa contratos por BancoId
        IEnumerable<IGrouping<Guid, Contrato>> grupos = ativos.GroupBy(c => c.BancoId);

        List<SaldoBancoAtualDto> bancos = new(capacity: ativos.Count);

        foreach (IGrouping<Guid, Contrato> grupo in grupos)
        {
            decimal saldoBrl = CalcularSaldoBrl(grupo, taxasPorMoeda);

            // 5. Busca dados cadastrais do banco (apelido, código COMPE)
            Banco? banco = await bancoRepo.GetByIdAsync(grupo.Key, cancellationToken);

            // Banco pode ter sido deletado logicamente sem que os contratos tenham sido encerrados.
            // Neste caso, usa valores de fallback em vez de omitir a linha — o saldo existe.
            string apelido = banco?.Apelido ?? grupo.Key.ToString("N");
            string codigoCompe = banco?.CodigoCompe ?? "???";

            bancos.Add(new SaldoBancoAtualDto(
                BancoId: grupo.Key,
                BancoApelido: apelido,
                BancoCodigoCompe: codigoCompe,
                SaldoBrl: Math.Round(saldoBrl, 2, MidpointRounding.AwayFromZero),
                QuantidadeContratosAtivos: grupo.Count()));
        }

        // 6. Total é a soma dos saldos individuais (sem double-rounding)
        decimal saldoTotal = Math.Round(
            bancos.Sum(b => b.SaldoBrl),
            2,
            MidpointRounding.AwayFromZero);

        return new SaldoPorBancoAtualDto(
            Bancos: bancos.AsReadOnly(),
            SaldoTotalBrl: saldoTotal,
            DataReferencia: hoje);
    }

    /// <summary>
    /// Calcula o saldo BRL de um grupo de contratos do mesmo banco.
    /// Contratos BRL não sofrem conversão (taxa = 1). Contratos em moeda estrangeira
    /// usam a taxa já resolvida. Se nenhuma taxa disponível para a moeda, contribui com zero.
    /// </summary>
    private static decimal CalcularSaldoBrl(
        IGrouping<Guid, Contrato> grupo,
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
    /// Estratégia de resolução de taxa idêntica ao <see cref="GetPainelDividaQueryHandler"/>:
    /// spot intraday Redis → PTAX D-1 mid-rate → omite (taxa zero, contrato contribui com zero BRL).
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

            CotacaoFx? ptax = await cotacaoFxRepo.GetMaisRecenteAsync(
                moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);

            if (ptax is not null)
            {
                // Mid-rate: (compra + venda) / 2 — consistente com GetPainelDividaQueryHandler
                decimal midRate = Math.Round(
                    (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                    6,
                    MidpointRounding.AwayFromZero);
                resultado[moeda] = midRate;
            }
            // Se nem spot nem PTAX disponível, não registra — contribuirá com zero BRL
        }

        return resultado;
    }
}
