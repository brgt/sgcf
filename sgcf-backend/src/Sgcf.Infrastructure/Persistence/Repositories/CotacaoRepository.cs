using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ICotacaoRepository"/> usando EF Core + PostgreSQL.
/// Apenas persistência — zero lógica de domínio.
/// </summary>
internal sealed class CotacaoRepository(SgcfDbContext context) : ICotacaoRepository
{
    public void Add(Cotacao cotacao) => context.Cotacoes.Add(cotacao);

    public void Update(Cotacao cotacao) => context.Cotacoes.Update(cotacao);

    public void Remove(Cotacao cotacao) => context.Cotacoes.Remove(cotacao);

    public Task<Cotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Cotacoes
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Cotacao?> GetByIdWithPropostasAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Cotacoes
            .Include(c => c.Propostas)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Cotacao> Items, int Total)> ListPagedAsync(
        CotacaoFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Cotacao> q = context.Cotacoes.AsNoTracking();

        if (filter.Status.HasValue)
        {
            q = q.Where(c => c.Status == filter.Status.Value);
        }

        if (filter.Modalidade.HasValue)
        {
            q = q.Where(c => c.Modalidade == filter.Modalidade.Value);
        }

        if (filter.Desde.HasValue)
        {
            q = q.Where(c => c.DataAbertura >= filter.Desde.Value);
        }

        if (filter.Ate.HasValue)
        {
            q = q.Where(c => c.DataAbertura <= filter.Ate.Value);
        }

        q = q.OrderByDescending(c => c.DataAbertura)
              .ThenByDescending(c => c.CreatedAt);

        int total = await q.CountAsync(cancellationToken);
        List<Cotacao> items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), total);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Gera o próximo código interno para o ano dado.
    /// Busca o maior número sequencial já usado em COT-{ano}-NNNNN e incrementa.
    /// Thread-safety: no MVP sem advisory lock; colisão improvável em uso single-tenant.
    /// SPEC §13 decisão 4.
    /// </summary>
    public async Task<string> GerarProximoCodigoInternoAsync(int ano, CancellationToken cancellationToken = default)
    {
        string prefixo = $"COT-{ano}-";
        int prefixoLen = prefixo.Length;

        // Busca ignorando soft delete para garantir que o próximo código não colida
        // com códigos de cotações já deletadas (SPEC §12.3).
        // Traz apenas os sufixos para o cliente para evitar int.Parse no LINQ-to-SQL.
        List<string> sufixos = await context.Cotacoes
            .IgnoreQueryFilters()
            .Where(c => c.CodigoInterno.StartsWith(prefixo))
            .Select(c => c.CodigoInterno.Substring(prefixoLen))
            .ToListAsync(cancellationToken);

        int maxSeq = sufixos
            .Select(s => int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        int proximo = maxSeq + 1;
        return $"{prefixo}{proximo:D5}";
    }
}
