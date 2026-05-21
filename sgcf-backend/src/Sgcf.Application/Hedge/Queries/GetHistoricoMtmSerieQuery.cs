using System.Globalization;
using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Hedge;

namespace Sgcf.Application.Hedge.Queries;

/// <summary>
/// Retorna a série histórica de snapshots de MtM para um hedge no intervalo [De, Ate].
///
/// <para>Quando <see cref="De"/> é nulo, o padrão é hoje BRT menos 90 dias.</para>
/// <para>Quando <see cref="Ate"/> é nulo, o padrão é hoje BRT.</para>
/// <para>O handler lança <see cref="KeyNotFoundException"/> quando o hedge não existe no tenant.</para>
/// </summary>
public sealed record GetHistoricoMtmSerieQuery(
    Guid HedgeId,
    string? De = null,
    string? Ate = null) : IRequest<EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>>>;

/// <summary>
/// Processa <see cref="GetHistoricoMtmSerieQuery"/> e retorna a série histórica envelopada
/// com metadados de observabilidade.
/// </summary>
public sealed class GetHistoricoMtmSerieQueryHandler(
    IHedgeRepository hedgeRepo,
    IHistoricoMtmRepository historicoRepo,
    IClock clock)
    : IRequestHandler<GetHistoricoMtmSerieQuery, EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>>> Handle(
        GetHistoricoMtmSerieQuery query,
        CancellationToken cancellationToken)
    {
        // Confirma existência do hedge no tenant antes de consultar o histórico.
        _ = await hedgeRepo.GetByIdAsync(query.HedgeId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Instrumento de hedge com Id '{query.HedgeId}' não encontrado.");

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        LocalDate de  = string.IsNullOrWhiteSpace(query.De)
            ? hoje.PlusDays(-90)
            : LocalDate.FromDateTime(DateTime.Parse(query.De, CultureInfo.InvariantCulture));

        LocalDate ate = string.IsNullOrWhiteSpace(query.Ate)
            ? hoje
            : LocalDate.FromDateTime(DateTime.Parse(query.Ate, CultureInfo.InvariantCulture));

        IReadOnlyList<HistoricoMtmDiario> registros = await historicoRepo.ListByHedgeIdAsync(
            query.HedgeId,
            de,
            ate,
            cancellationToken);

        IReadOnlyList<HistoricoMtmDiarioDto> dtos = registros
            .Select(ToDto)
            .ToList()
            .AsReadOnly();

        EnvelopeMeta meta = new(
            DataHoraCalculo:   clock.GetCurrentInstant(),
            FontesConsultadas: [new FonteConsultada("banco_de_dados", "ok", registros.Count)],
            Completude:        Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<HistoricoMtmDiarioDto>>(dtos, meta);
    }

    private static HistoricoMtmDiarioDto ToDto(HistoricoMtmDiario h)
    {
        decimal payoff = h.PayoffBrl.Valor;
        return new(
            DataReferencia: h.DataReferencia.ToString("yyyy-MM-dd", null),
            PayoffBrl:      payoff,
            Posicao:        payoff > 0 ? "RECEBER" : payoff < 0 ? "PAGAR" : "NEUTRO",
            SpotUtilizado:  h.SpotUtilizado,
            TipoCotacao:    h.TipoCotacao);
    }
}
