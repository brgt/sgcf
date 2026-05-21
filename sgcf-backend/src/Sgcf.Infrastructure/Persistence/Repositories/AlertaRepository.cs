using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgcf.Application.Alertas;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IAlertaRepository"/> usando EF Core + PostgreSQL.
/// O global query filter de tenant é aplicado automaticamente pelo <see cref="SgcfDbContext"/>.
/// </summary>
internal sealed class AlertaRepository(SgcfDbContext context) : IAlertaRepository
{
    // Código de erro do PostgreSQL para violação de unique constraint.
    private const string PostgresUniqueViolationCode = "23505";

    public Task<Alerta?> GetByIdAsync(Guid id, CancellationToken ct) =>
        context.Alertas
            .Include(a => a.PerfisVisiveis)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Alerta>> ListAsync(AlertaFilter filter, CancellationToken ct)
    {
        IQueryable<Alerta> query = context.Alertas
            .Include(a => a.PerfisVisiveis)
            .AsNoTracking();

        if (filter.Perfil.HasValue)
        {
            PerfilCockpit perfil = filter.Perfil.Value;
            query = query.Where(a => a.PerfisVisiveis.Any(p => p.Perfil == perfil));
        }

        if (filter.Severidade.HasValue)
        {
            query = query.Where(a => a.Severidade == filter.Severidade.Value);
        }

        if (filter.Categoria.HasValue)
        {
            query = query.Where(a => a.Categoria == filter.Categoria.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(a => a.Status == filter.Status.Value);
        }

        List<Alerta> alertas = await query
            .OrderBy(a => a.Severidade)
            .ThenByDescending(a => a.CriadoEm)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return alertas;
    }

    public async Task<ContadoresAlerta> GetContadoresAsync(PerfilCockpit perfil, CancellationToken ct)
    {
        // Consulta projetada: lê apenas (severidade, status) sem carregar o resto do agregado.
        // O global filter garante isolamento por tenant automaticamente.
        var contagens = await context.Alertas
            .AsNoTracking()
            .Where(a => a.Status == StatusAlerta.Aberto
                     && a.PerfisVisiveis.Any(p => p.Perfil == perfil))
            .GroupBy(a => a.Severidade)
            .Select(g => new { Severidade = g.Key, Contagem = g.Count() })
            .ToListAsync(ct);

        int critico    = contagens.FirstOrDefault(c => c.Severidade == SeveridadeAlerta.Critico)?.Contagem    ?? 0;
        int atencao    = contagens.FirstOrDefault(c => c.Severidade == SeveridadeAlerta.Atencao)?.Contagem    ?? 0;
        int informativo = contagens.FirstOrDefault(c => c.Severidade == SeveridadeAlerta.Informativo)?.Contagem ?? 0;

        return new ContadoresAlerta(critico, atencao, informativo);
    }

    public async Task AddAsync(Alerta alerta, CancellationToken ct)
    {
        await context.Alertas.AddAsync(alerta, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);

    public async Task<bool> TryAddIdempotentAsync(Alerta alerta, CancellationToken ct)
    {
        try
        {
            await context.Alertas.AddAsync(alerta, ct);
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pgEx
               && pgEx.SqlState == PostgresUniqueViolationCode)
        {
            // Violação da unique constraint em chave_idempotencia — alerta já existe.
            // Desfaz o rastreamento da entidade para não poluir o contexto.
            context.Entry(alerta).State = EntityState.Detached;
            return false;
        }
    }
}
