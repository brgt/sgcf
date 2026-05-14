using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos;

public interface IBancoRepository
{
    public Task<Banco?> GetByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Banco?> GetByCodigoCompeAsync(string codigoCompe, CancellationToken ct = default);
    public Task<Banco?> GetByApelidoAsync(string apelido, CancellationToken ct = default);
    public Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna bancos filtrando por texto livre (ILIKE em CodigoCompe, RazaoSocial e Apelido).
    /// Quando <paramref name="search"/> é nulo ou vazio, retorna todos os bancos.
    /// </summary>
    public Task<IReadOnlyList<Banco>> ListFilteredAsync(string? search, CancellationToken ct = default);
    public void Add(Banco banco);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna todos os bancos que têm LimiteCreditoBrl configurado (não nulo).
    /// Usado pelo job de alerta de exposição de banco.
    /// </summary>
    public Task<IReadOnlyList<Banco>> ListComLimiteCreditoSetadoAsync(CancellationToken ct = default);
}
