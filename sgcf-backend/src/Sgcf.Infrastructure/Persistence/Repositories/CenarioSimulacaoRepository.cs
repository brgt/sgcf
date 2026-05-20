using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Simulacao;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ICenarioSimulacaoRepository"/> usando EF Core + PostgreSQL.
///
/// Convenções:
/// - Add/Update/Remove não persistem imediatamente — é necessário chamar SaveChangesAsync.
/// - GetByIdAsync e ListAsync aplicam o query filter (deleted_at IS NULL) automaticamente.
///   Para acessar registros deletados use IgnoreQueryFilters() diretamente no DbContext.
/// - Remove faz hard delete físico. Para soft delete, chame cenario.Deletar(clock) + Update.
/// </summary>
internal sealed class CenarioSimulacaoRepository(SgcfDbContext context) : ICenarioSimulacaoRepository
{
    /// <inheritdoc/>
    public void Add(CenarioSimulacao cenario) =>
        context.CenariosSimulacao.Add(cenario);

    /// <inheritdoc/>
    public void Update(CenarioSimulacao cenario) =>
        context.CenariosSimulacao.Update(cenario);

    /// <inheritdoc/>
    public void Remove(CenarioSimulacao cenario) =>
        context.CenariosSimulacao.Remove(cenario);

    /// <inheritdoc/>
    /// <remarks>
    /// Usa tracking (sem AsNoTracking) para permitir que o chamador modifique
    /// o agregado e chame Update/SaveChanges sem DbUpdateConcurrencyException.
    /// O EF rastreia o estado original e detecta corretamente novas simulações filhas vs. existentes.
    /// </remarks>
    public Task<CenarioSimulacao?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.CenariosSimulacao
            .Include(c => c.Simulacoes)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CenarioSimulacao>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<CenarioSimulacao>();
        }

        List<CenarioSimulacao> carregados = await context.CenariosSimulacao
            .Include(c => c.Simulacoes)
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        // Preservar a ordem da entrada (o primeiro id é a baseline na comparação)
        Dictionary<Guid, CenarioSimulacao> porId = carregados.ToDictionary(c => c.Id);
        var ordenados = new List<CenarioSimulacao>(ids.Count);
        foreach (Guid id in ids)
        {
            if (porId.TryGetValue(id, out CenarioSimulacao? c))
            {
                ordenados.Add(c);
            }
        }
        return ordenados.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CenarioSimulacao>> ListAsync(
        StatusCenarioSimulacao? status,
        int? anoBase,
        string? criadoPor,
        CancellationToken ct = default)
    {
        IQueryable<CenarioSimulacao> q = context.CenariosSimulacao
            .AsNoTracking();

        if (status.HasValue)
        {
            q = q.Where(c => c.Status == status.Value);
        }

        if (anoBase.HasValue)
        {
            q = q.Where(c => c.AnoBase == anoBase.Value);
        }

        if (!string.IsNullOrWhiteSpace(criadoPor))
        {
            q = q.Where(c => c.CriadoPor == criadoPor);
        }

        List<CenarioSimulacao> list = await q
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
