using MediatR;
using NodaTime;
using Sgcf.Application.Common;

namespace Sgcf.Application.Auditoria.Queries;

/// <summary>
/// Retorna a produtividade da equipe de analistas no intervalo de meses especificado,
/// calculada a partir do AuditLog. Inclui total de operações por analista e SLA médio
/// de atendimento por entidade.
/// </summary>
public sealed record GetProdutividadeEquipeQuery(
    int DeAno, int DeMes,
    int AteAno, int AteMes) : IRequest<EnvelopeResponse<IReadOnlyList<ProdutividadeAnalistaDto>>>;

public sealed class GetProdutividadeEquipeQueryHandler(
    IAuditLogRepository repository,
    IClock clock)
    : IRequestHandler<GetProdutividadeEquipeQuery, EnvelopeResponse<IReadOnlyList<ProdutividadeAnalistaDto>>>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    /// <inheritdoc/>
    public async Task<EnvelopeResponse<IReadOnlyList<ProdutividadeAnalistaDto>>> Handle(
        GetProdutividadeEquipeQuery query,
        CancellationToken cancellationToken)
    {
        if (query.DeMes is < 1 or > 12 || query.AteMes is < 1 or > 12)
        {
            throw new ArgumentException("Mês deve estar entre 1 e 12.");
        }

        if (query.DeAno > query.AteAno || (query.DeAno == query.AteAno && query.DeMes > query.AteMes))
        {
            throw new ArgumentException("Período inválido: data inicial deve ser anterior à data final.");
        }

        Instant agora = clock.GetCurrentInstant();

        // de = primeiro instante do mês DeAno/DeMes no fuso de Brasília.
        LocalDate deData = new LocalDate(query.DeAno, query.DeMes, 1);
        Instant de = deData.AtStartOfDayInZone(FusoBrasilia).ToInstant();

        // ate = primeiro instante do mês seguinte ao AteAno/AteMes no fuso de Brasília,
        // o que equivale ao último instante (exclusivo) do mês solicitado.
        LocalDate ateData = new LocalDate(query.AteAno, query.AteMes, 1).PlusMonths(1);
        Instant ate = ateData.AtStartOfDayInZone(FusoBrasilia).ToInstant();

        IReadOnlyList<ProdutividadeAnalistaDto> produtividade =
            await repository.GetProdutividadeAsync(de, ate, cancellationToken);

        EnvelopeMeta meta = new(
            agora,
            [new FonteConsultada("audit_log", "ok", produtividade.Count)],
            Completude.Completo);

        return new EnvelopeResponse<IReadOnlyList<ProdutividadeAnalistaDto>>(produtividade, meta);
    }
}
