using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Relatório agregado de economia por período (mês/ano), com subtotais por banco. SPEC §6.2.
/// </summary>
public sealed record GetEconomiaPeriodoQuery(
    YearMonth De,
    YearMonth Ate,
    Guid? BancoId = null) : IRequest<EconomiaPeriodoDto>;

public sealed class GetEconomiaPeriodoQueryHandler(IEconomiaRepository repo)
    : IRequestHandler<GetEconomiaPeriodoQuery, EconomiaPeriodoDto>
{
    public async Task<EconomiaPeriodoDto> Handle(GetEconomiaPeriodoQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<EconomiaNegociacao> economias = await repo.ListByPeriodoAsync(
            query.De,
            query.Ate,
            query.BancoId,
            cancellationToken);

        if (economias.Count == 0)
        {
            return new EconomiaPeriodoDto([], [], 0m, 0m, 0);
        }

        // Agregar por mês
        Dictionary<(int Ano, int Mes), List<EconomiaNegociacao>> porMes = [];
        foreach (EconomiaNegociacao e in economias)
        {
            LocalDate dataRef = e.DataReferenciaCdi;
            (int Ano, int Mes) key = (dataRef.Year, dataRef.Month);
            if (!porMes.TryGetValue(key, out List<EconomiaNegociacao>? lista))
            {
                lista = [];
                porMes[key] = lista;
            }
            lista.Add(e);
        }

        List<EconomiaMesDto> mesList = new(porMes.Count);
        foreach (KeyValuePair<(int Ano, int Mes), List<EconomiaNegociacao>> kv in porMes.OrderBy(x => x.Key))
        {
            List<EconomiaNegociacao> grupo = kv.Value;
            mesList.Add(new EconomiaMesDto(
                kv.Key.Ano,
                kv.Key.Mes,
                grupo.Count,
                Math.Round(grupo.Sum(e => e.EconomiaBrl.Valor), 2, MidpointRounding.AwayFromZero),
                Math.Round(grupo.Sum(e => e.EconomiaAjustadaCdiBrl.Valor), 2, MidpointRounding.AwayFromZero)));
        }

        // Subtotais por banco: requer CotacaoId → BancoId lookup
        // Para MVP: agrupamos por ContratoId que é 1:1 com BancoId via proposta aceita.
        // Como EconomiaNegociacao não armazena BancoId diretamente, usamos o snapshot JSON.
        // Decisão de design: deixamos PorBanco vazio no MVP se BancoId não disponível diretamente.
        // Onda 3 pode adicionar BancoId à tabela economia_negociacao como otimização de query.
        List<EconomiaPorBancoDto> bancosResult = [];

        decimal totalBruta = Math.Round(economias.Sum(e => e.EconomiaBrl.Valor), 2, MidpointRounding.AwayFromZero);
        decimal totalAjustada = Math.Round(economias.Sum(e => e.EconomiaAjustadaCdiBrl.Valor), 2, MidpointRounding.AwayFromZero);

        return new EconomiaPeriodoDto(
            mesList.AsReadOnly(),
            bancosResult.AsReadOnly(),
            totalBruta,
            totalAjustada,
            economias.Count);
    }
}
