using NodaTime;
using Sgcf.Domain.Benchmarks;

namespace Sgcf.Application.Benchmarks;

public interface ITaxaBenchmarkRepository
{
    public Task<TaxaBenchmark?> GetAsync(string tipo, LocalDate data, CancellationToken ct = default);
    public Task<IReadOnlyList<TaxaBenchmark>> ListAsync(string tipo, LocalDate de, LocalDate ate, CancellationToken ct = default);
    public void Add(TaxaBenchmark t);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
