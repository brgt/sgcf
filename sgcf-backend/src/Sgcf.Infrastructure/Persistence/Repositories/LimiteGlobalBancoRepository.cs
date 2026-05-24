using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ILimiteGlobalBancoRepository"/> usando EF Core + PostgreSQL.
/// </summary>
internal sealed class LimiteGlobalBancoRepository(SgcfDbContext context) : ILimiteGlobalBancoRepository
{
    public void Add(LimiteGlobalBanco limite) => context.LimitesGlobaisBanco.Add(limite);

    /// <inheritdoc/>
    public Task<LimiteGlobalBanco?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.LimitesGlobaisBanco
            .Include(l => l.Historico)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc/>
    public Task<LimiteGlobalBanco?> GetByIdTrackingAsync(Guid id, CancellationToken ct = default) =>
        context.LimitesGlobaisBanco
            .Include(l => l.Historico)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <summary>
    /// Retorna o limite vigente (sem DataVigenciaFim) para o banco.
    /// "Vigente" é definido como DataVigenciaFim == null (sem encerramento programado).
    /// Historico é carregado eagerly para que operações de leitura funcionem sem lazy-loading.
    /// </summary>
    public Task<LimiteGlobalBanco?> GetVigenteByBancoAsync(Guid bancoId, CancellationToken ct = default) =>
        context.LimitesGlobaisBanco
            .Include(l => l.Historico)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId && l.DataVigenciaFim == null,
                ct);

    /// <summary>
    /// Retorna o primeiro limite que se sobrepõe ao período [inicio, fim] para o banco.
    /// Overlap: (fim == null || fim >= l.DataVigenciaInicio) AND (l.DataVigenciaFim == null || l.DataVigenciaFim >= inicio).
    /// </summary>
    public Task<LimiteGlobalBanco?> FindOverlappingAsync(
        Guid bancoId,
        LocalDate inicio,
        LocalDate? fim,
        Guid? excluirId = null,
        CancellationToken ct = default) =>
        context.LimitesGlobaisBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId
                  && (excluirId == null || l.Id != excluirId.Value)
                  // l.Inicio <= fim (null fim = +∞, always true)
                  && (fim == null || l.DataVigenciaInicio <= fim.Value)
                  // inicio <= l.Fim (null l.Fim = +∞, always true)
                  && (l.DataVigenciaFim == null || inicio <= l.DataVigenciaFim.Value),
                ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LimiteGlobalBanco>> ListAsync(
        Guid? bancoId,
        LocalDate? vigentesEm,
        CancellationToken ct = default)
    {
        IQueryable<LimiteGlobalBanco> q = context.LimitesGlobaisBanco
            .Include(l => l.Historico)
            .AsNoTracking();

        if (bancoId.HasValue)
        {
            q = q.Where(l => l.BancoId == bancoId.Value);
        }

        if (vigentesEm.HasValue)
        {
            // Inclui apenas registros cujo período [DataVigenciaInicio, DataVigenciaFim] contém a data informada.
            q = q.Where(l =>
                l.DataVigenciaInicio <= vigentesEm.Value
                && (l.DataVigenciaFim == null || l.DataVigenciaFim >= vigentesEm.Value));
        }

        List<LimiteGlobalBanco> list = await q
            .OrderBy(l => l.BancoId)
            .ThenBy(l => l.DataVigenciaInicio)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
