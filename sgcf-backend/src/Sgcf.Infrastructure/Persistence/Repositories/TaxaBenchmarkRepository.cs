using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Benchmarks;
using Sgcf.Domain.Benchmarks;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class TaxaBenchmarkRepository(SgcfDbContext context) : ITaxaBenchmarkRepository
{
    public Task<TaxaBenchmark?> GetAsync(string tipo, LocalDate data, CancellationToken ct) =>
        context.TaxasBenchmark
            .FirstOrDefaultAsync(t => t.TipoBenchmark == tipo && t.DataReferencia == data, ct);

    public async Task<IReadOnlyList<TaxaBenchmark>> ListAsync(
        string tipo,
        LocalDate de,
        LocalDate ate,
        CancellationToken ct)
    {
        List<TaxaBenchmark> list = await context.TaxasBenchmark
            .Where(t => t.TipoBenchmark == tipo && t.DataReferencia >= de && t.DataReferencia <= ate)
            .OrderBy(t => t.DataReferencia)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public void Add(TaxaBenchmark t) => context.TaxasBenchmark.Add(t);

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
