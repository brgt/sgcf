using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Preferencias;
using Sgcf.Domain.Preferencias;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IPreferenciaUsuarioRepository"/>.
///
/// O EF Core global query filter (tenant) garante que todas as queries
/// retornam apenas dados do tenant ativo no contexto atual.
/// Nenhum parâmetro tenantId é passado explicitamente — confiar no filter.
/// </summary>
internal sealed class PreferenciaUsuarioRepository(SgcfDbContext context) : IPreferenciaUsuarioRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PreferenciaUsuario>> ListByUserIdAsync(string userId, CancellationToken ct) =>
        await context.Set<PreferenciaUsuario>()
            .Where(p => p.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<PreferenciaUsuario?> GetAsync(string userId, string chave, CancellationToken ct) =>
        context.Set<PreferenciaUsuario>()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Chave == chave, ct);

    /// <inheritdoc />
    public void Add(PreferenciaUsuario p) =>
        context.Set<PreferenciaUsuario>().Add(p);

    /// <inheritdoc />
    public void Remove(PreferenciaUsuario p) =>
        context.Set<PreferenciaUsuario>().Remove(p);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
