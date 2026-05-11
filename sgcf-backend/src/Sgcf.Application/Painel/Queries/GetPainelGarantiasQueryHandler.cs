using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Consolida todas as garantias ativas do portfólio, distribui por tipo e por banco,
/// e emite alertas de vencimento iminente para CDB (30 dias) e Boleto (2 dias).
/// </summary>
public sealed class GetPainelGarantiasQueryHandler(
    IContratoRepository contratoRepo,
    IGarantiaRepository garantiaRepo,
    IClock clock)
    : IRequestHandler<GetPainelGarantiasQuery, PainelGarantiasDto>
{
    private const int DiasAlertaCdb = 30;
    private const int DiasAlertaBoleto = 2;

    public async Task<PainelGarantiasDto> Handle(
        GetPainelGarantiasQuery query,
        CancellationToken cancellationToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;
        string dataCalculo = hoje.ToString();

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(cancellationToken);

        // Coleta todas as garantias ativas de todos os contratos
        List<(Garantia garantia, Guid bancoId, string numeroContrato)> garantiasComContexto = new();

        foreach (Contrato contrato in contratos)
        {
            foreach (Garantia garantia in contrato.Garantias)
            {
                if (garantia.Status == StatusGarantia.Ativa)
                {
                    garantiasComContexto.Add((garantia, contrato.BancoId, contrato.NumeroExterno));
                }
            }
        }

        decimal total = garantiasComContexto.Sum(g => g.garantia.ValorBrl.Valor);

        IReadOnlyList<LinhaDistribuicaoTipoDto> distribuicaoPorTipo =
            ComputarDistribuicaoPorTipo(garantiasComContexto, total);

        IReadOnlyList<LinhaDistribuicaoBancoDto> distribuicaoPorBanco =
            ComputarDistribuicaoPorBanco(garantiasComContexto, total);

        IReadOnlyList<string> alertas = await GerarAlertasAsync(
            garantiasComContexto, hoje, cancellationToken);

        return new PainelGarantiasDto(
            DataCalculo: dataCalculo,
            TotalGarantiasAtivasBrl: Math.Round(total, 2, MidpointRounding.AwayFromZero),
            DistribuicaoPorTipo: distribuicaoPorTipo,
            DistribuicaoPorBanco: distribuicaoPorBanco,
            Alertas: alertas);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<LinhaDistribuicaoTipoDto> ComputarDistribuicaoPorTipo(
        List<(Garantia garantia, Guid bancoId, string numeroContrato)> garantias,
        decimal total)
    {
        List<LinhaDistribuicaoTipoDto> linhas = garantias
            .GroupBy(g => g.garantia.Tipo)
            .Select(grupo =>
            {
                decimal valorGrupo = Math.Round(
                    grupo.Sum(g => g.garantia.ValorBrl.Valor),
                    6,
                    MidpointRounding.AwayFromZero);

                decimal percentual = total > 0m
                    ? Math.Round(valorGrupo / total * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new LinhaDistribuicaoTipoDto(
                    Tipo: grupo.Key.ToString(),
                    ValorBrl: Math.Round(valorGrupo, 2, MidpointRounding.AwayFromZero),
                    PercentualDoTotal: percentual);
            })
            .OrderByDescending(l => l.ValorBrl)
            .ToList();

        return linhas.AsReadOnly();
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<LinhaDistribuicaoBancoDto> ComputarDistribuicaoPorBanco(
        List<(Garantia garantia, Guid bancoId, string numeroContrato)> garantias,
        decimal total)
    {
        List<LinhaDistribuicaoBancoDto> linhas = garantias
            .GroupBy(g => g.bancoId)
            .Select(grupo =>
            {
                decimal valorGrupo = Math.Round(
                    grupo.Sum(g => g.garantia.ValorBrl.Valor),
                    6,
                    MidpointRounding.AwayFromZero);

                decimal percentual = total > 0m
                    ? Math.Round(valorGrupo / total * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new LinhaDistribuicaoBancoDto(
                    BancoId: grupo.Key,
                    ValorBrl: Math.Round(valorGrupo, 2, MidpointRounding.AwayFromZero),
                    PercentualDoTotal: percentual);
            })
            .OrderByDescending(l => l.ValorBrl)
            .ToList();

        return linhas.AsReadOnly();
    }

    private async Task<IReadOnlyList<string>> GerarAlertasAsync(
        List<(Garantia garantia, Guid bancoId, string numeroContrato)> garantias,
        LocalDate hoje,
        CancellationToken cancellationToken)
    {
        LocalDate limiteCdb = hoje.PlusDays(DiasAlertaCdb);
        LocalDate limiteBoleto = hoje.PlusDays(DiasAlertaBoleto);

        List<string> alertas = new();

        IEnumerable<(Garantia garantia, Guid bancoId, string numeroContrato)> cdbsAtivos =
            garantias.Where(g => g.garantia.Tipo == TipoGarantia.CdbCativo);

        foreach ((Garantia garantia, Guid _, string numeroContrato) in cdbsAtivos)
        {
            IReadOnlyList<GarantiaCdbCativoDetail> cdbs =
                await garantiaRepo.ListCdbAtivosComVencimentoAteAsync(garantia.Id, limiteCdb, cancellationToken);

            foreach (GarantiaCdbCativoDetail detalhe in cdbs)
            {
                int dias = Period.Between(hoje, detalhe.DataVencimentoCdb, PeriodUnits.Days).Days;
                alertas.Add(
                    $"CDB vencendo em {dias} dias — operação {numeroContrato} (CDB {detalhe.NumeroCdb})");
            }
        }

        IEnumerable<(Garantia garantia, Guid bancoId, string numeroContrato)> boletosAtivos =
            garantias.Where(g => g.garantia.Tipo == TipoGarantia.BoletoBancario);

        foreach ((Garantia garantia, Guid _, string numeroContrato) in boletosAtivos)
        {
            IReadOnlyList<GarantiaBoletoBancarioDetail> boletos =
                await garantiaRepo.ListBoletosAtivosComVencimentoAteAsync(garantia.Id, limiteBoleto, cancellationToken);

            foreach (GarantiaBoletoBancarioDetail detalhe in boletos)
            {
                int dias = Period.Between(hoje, detalhe.DataVencimentoFinal, PeriodUnits.Days).Days;
                alertas.Add(
                    $"Boleto vencendo em {dias} dias — operação {numeroContrato}");
            }
        }

        return alertas.AsReadOnly();
    }
}
