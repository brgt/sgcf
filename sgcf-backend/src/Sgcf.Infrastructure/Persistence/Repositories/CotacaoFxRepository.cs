using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Infrastructure.Persistence;

namespace Sgcf.Infrastructure.Persistence.Repositories;

public sealed class CotacaoFxRepository(SgcfDbContext context) : ICotacaoFxRepository
{
    public async Task UpsertAsync(CotacaoFx cotacao, CancellationToken cancellationToken = default)
    {
        bool exists = await context.CotacoesFx
            .AnyAsync(
                c => c.MoedaBase == cotacao.MoedaBase
                  && c.Momento == cotacao.Momento
                  && c.Tipo == cotacao.Tipo,
                cancellationToken);

        if (!exists)
        {
            context.CotacoesFx.Add(cotacao);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<CotacaoFx?> GetMaisRecenteAsync(
        Moeda moeda,
        TipoCotacao tipo,
        LocalDate dataMaxima,
        CancellationToken cancellationToken = default)
    {
        Instant limite = dataMaxima.PlusDays(1).AtMidnight().InUtc().ToInstant();

        return context.CotacoesFx
            .Where(c => c.MoedaBase == moeda && c.MoedaQuote == Moeda.Brl && c.Tipo == tipo && c.Momento < limite)
            .OrderByDescending(c => c.Momento)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
