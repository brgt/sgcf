using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IEconomiaRepository"/> usando EF Core + PostgreSQL.
/// </summary>
internal sealed class EconomiaRepository(SgcfDbContext context) : IEconomiaRepository
{
    public void Add(EconomiaNegociacao economia) => context.EconomiasNegociacao.Add(economia);

    public Task<EconomiaNegociacao?> GetByCotacaoIdAsync(
        Guid cotacaoId,
        CancellationToken cancellationToken = default) =>
        context.EconomiasNegociacao
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CotacaoId == cotacaoId, cancellationToken);

    /// <summary>
    /// Lista economias cujo <c>CreatedAt</c> está entre os meses <paramref name="de"/> e
    /// <paramref name="ate"/> (inclusivo, baseado em início e fim de mês).
    /// O filtro opcional <paramref name="bancoId"/> cruza com a proposta aceita via subquery
    /// na tabela de propostas.
    /// SPEC §6.2 GetEconomiaPeriodoQuery.
    /// </summary>
    public async Task<IReadOnlyList<EconomiaNegociacao>> ListByPeriodoAsync(
        YearMonth de,
        YearMonth ate,
        Guid? bancoId,
        CancellationToken cancellationToken = default)
    {
        // Converter YearMonth para Instant boundaries (início do mês de início, fim do mês de fim).
        Instant inicioPeriodo = de.OnDayOfMonth(1).AtMidnight().InUtc().ToInstant();
        Instant fimPeriodo = ate
            .OnDayOfMonth(1)
            .PlusMonths(1)
            .AtMidnight()
            .InUtc()
            .ToInstant();

        IQueryable<EconomiaNegociacao> q = context.EconomiasNegociacao
            .AsNoTracking()
            .Where(e => e.CreatedAt >= inicioPeriodo && e.CreatedAt < fimPeriodo);

        if (bancoId.HasValue)
        {
            Guid banco = bancoId.Value;
            // Filtra por banco via proposta aceita: o cotacao_id da economia liga à cotacao,
            // que tem proposta_aceita_id que aponta para uma proposta com banco_id.
            q = q.Where(e =>
                context.Propostas.Any(p =>
                    p.CotacaoId == e.CotacaoId
                    && p.BancoId == banco
                    && (int)p.Status == 2)); // StatusProposta.Aceita == 2
        }

        List<EconomiaNegociacao> list = await q
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
