using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos;

public interface IBancoRepository
{
    public Task<Banco?> GetByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Banco?> GetByCodigoCompeAsync(string codigoCompe, CancellationToken ct = default);
    public Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct = default);
    public void Add(Banco banco);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
