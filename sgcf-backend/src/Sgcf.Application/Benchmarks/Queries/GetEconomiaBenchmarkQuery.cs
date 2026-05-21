using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Benchmarks;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Benchmarks.Queries;

public sealed record GetEconomiaBenchmarkQuery(
    YearMonth De,
    YearMonth Ate,
    string Benchmark,
    Guid? BancoId = null)
    : IRequest<EnvelopeResponse<EconomiaBenchmarkDto>>;

public sealed class GetEconomiaBenchmarkQueryHandler(
    IEconomiaRepository economiaRepository,
    ITaxaBenchmarkRepository taxaRepository,
    IClock clock)
    : IRequestHandler<GetEconomiaBenchmarkQuery, EnvelopeResponse<EconomiaBenchmarkDto>>
{
    public async Task<EnvelopeResponse<EconomiaBenchmarkDto>> Handle(
        GetEconomiaBenchmarkQuery query,
        CancellationToken cancellationToken)
    {
        Instant agora = clock.GetCurrentInstant();

        IReadOnlyList<EconomiaNegociacao> economias = await economiaRepository.ListByPeriodoAsync(
            query.De,
            query.Ate,
            query.BancoId,
            cancellationToken);

        int operacoesSemTaxa = 0;
        Dictionary<(int Ano, int Mes), (int Count, decimal EconomiaBrl, decimal EconomiaBenchmarkBrl)> porMes = [];

        foreach (EconomiaNegociacao e in economias)
        {
            TaxaBenchmark? taxa = await taxaRepository.GetAsync(
                query.Benchmark,
                e.DataReferenciaCdi,
                cancellationToken);

            decimal economiaBenchmarkBrl;
            if (taxa is null)
            {
                operacoesSemTaxa++;
                economiaBenchmarkBrl = 0m;
            }
            else
            {
                decimal delta = e.CetPropostaAaPercentual - e.CetContratoAaPercentual;
                economiaBenchmarkBrl = delta == 0m
                    ? 0m
                    : Math.Round(
                        e.EconomiaBrl.Valor * (taxa.TaxaAa - e.CetContratoAaPercentual) / delta,
                        2, MidpointRounding.AwayFromZero);
            }

            (int Ano, int Mes) key = (e.DataReferenciaCdi.Year, e.DataReferenciaCdi.Month);
            if (porMes.TryGetValue(key, out var atual))
            {
                porMes[key] = (
                    atual.Count + 1,
                    atual.EconomiaBrl + e.EconomiaBrl.Valor,
                    atual.EconomiaBenchmarkBrl + economiaBenchmarkBrl);
            }
            else
            {
                porMes[key] = (1, e.EconomiaBrl.Valor, economiaBenchmarkBrl);
            }
        }

        List<EconomiaBenchmarkItemDto> itens = porMes
            .OrderBy(kv => kv.Key)
            .Select(kv => new EconomiaBenchmarkItemDto(
                kv.Key.Ano,
                kv.Key.Mes,
                kv.Value.Count,
                Math.Round(kv.Value.EconomiaBrl, 2, MidpointRounding.AwayFromZero),
                Math.Round(kv.Value.EconomiaBenchmarkBrl, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        decimal totalEconomiaBrl = Math.Round(
            itens.Sum(i => i.EconomiaBrl), 2, MidpointRounding.AwayFromZero);
        decimal totalEconomiaBenchmarkBrl = Math.Round(
            itens.Sum(i => i.EconomiaVsBenchmarkBrl), 2, MidpointRounding.AwayFromZero);

        EconomiaBenchmarkDto dto = new(
            query.Benchmark,
            itens.AsReadOnly(),
            totalEconomiaBrl,
            totalEconomiaBenchmarkBrl,
            economias.Count,
            operacoesSemTaxa);

        Completude completude = operacoesSemTaxa > 0 ? Completude.Parcial : Completude.Completo;

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("banco_de_dados", "ok", economias.Count)],
            completude);

        return new EnvelopeResponse<EconomiaBenchmarkDto>(dto, meta);
    }
}
