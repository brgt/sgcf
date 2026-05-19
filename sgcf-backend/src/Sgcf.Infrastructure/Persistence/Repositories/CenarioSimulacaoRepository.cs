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
    public Task<CenarioSimulacao?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.CenariosSimulacao
            .Include(c => c.Simulacoes)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

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
