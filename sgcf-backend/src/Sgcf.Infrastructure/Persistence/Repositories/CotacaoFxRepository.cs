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

    public async Task RegistrarOuAtualizarAsync(CotacaoFx cotacao, CancellationToken cancellationToken = default)
    {
        // Checagem alinhada à unique key (moeda_base, moeda_quote, momento, tipo).
        CotacaoFx? existente = await context.CotacoesFx
            .FirstOrDefaultAsync(
                c => c.MoedaBase == cotacao.MoedaBase
                  && c.MoedaQuote == cotacao.MoedaQuote
                  && c.Momento == cotacao.Momento
                  && c.Tipo == cotacao.Tipo,
                cancellationToken);

        if (existente is null)
        {
            context.CotacoesFx.Add(cotacao);
        }
        else
        {
            // Correção: atualiza valores in-place, preservando a chave e o Id.
            existente.AtualizarValores(cotacao.ValorCompra, cotacao.ValorVenda, cotacao.Fonte);
        }

        await context.SaveChangesAsync(cancellationToken);
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
