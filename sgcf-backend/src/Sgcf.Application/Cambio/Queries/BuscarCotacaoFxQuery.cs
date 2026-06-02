using MediatR;
using NodaTime;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Cambio.Queries;

/// <summary>
/// Conferência (leitura admin): retorna a <see cref="CotacaoFx"/> mais recente para
/// (moeda, tipo) com momento até <see cref="Ate"/> inclusive. Retorna null se não houver.
/// SPEC §4.2 (RF-09).
/// </summary>
public sealed record BuscarCotacaoFxQuery(
    Moeda Moeda,
    TipoCotacao Tipo,
    LocalDate Ate) : IRequest<CotacaoFxDto?>;

public sealed class BuscarCotacaoFxQueryHandler(ICotacaoFxRepository repo)
    : IRequestHandler<BuscarCotacaoFxQuery, CotacaoFxDto?>
{
    public async Task<CotacaoFxDto?> Handle(BuscarCotacaoFxQuery query, CancellationToken cancellationToken)
    {
        CotacaoFx? cotacao = await repo.GetMaisRecenteAsync(query.Moeda, query.Tipo, query.Ate, cancellationToken);
        return cotacao is null ? null : CotacaoFxDto.From(cotacao);
    }
}
