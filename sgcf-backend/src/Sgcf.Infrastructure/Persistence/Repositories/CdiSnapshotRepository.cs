using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ICdiSnapshotRepository"/> usando EF Core + PostgreSQL.
/// </summary>
internal sealed class CdiSnapshotRepository(SgcfDbContext context) : ICdiSnapshotRepository
{
    public void Add(CdiSnapshot snapshot) => context.CdiSnapshots.Add(snapshot);

    public Task<CdiSnapshot?> GetByDataAsync(
        LocalDate data,
        CancellationToken cancellationToken = default) =>
        context.CdiSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Data == data, cancellationToken);

    /// <summary>
    /// Retorna o snapshot mais recente em ou antes de <paramref name="dataMaxima"/>.
    /// Útil quando o CDI do dia exato não foi ainda cadastrado (feriado, fim de semana).
    /// </summary>
    public Task<CdiSnapshot?> GetMaisRecenteAsync(
        LocalDate dataMaxima,
        CancellationToken cancellationToken = default) =>
        context.CdiSnapshots
            .AsNoTracking()
            .Where(s => s.Data <= dataMaxima)
            .OrderByDescending(s => s.Data)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CdiSnapshot>> ListByPeriodoAsync(
        LocalDate de,
        LocalDate ate,
        CancellationToken cancellationToken = default)
    {
        List<CdiSnapshot> list = await context.CdiSnapshots
            .AsNoTracking()
            .Where(s => s.Data >= de && s.Data <= ate)
            .OrderBy(s => s.Data)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
