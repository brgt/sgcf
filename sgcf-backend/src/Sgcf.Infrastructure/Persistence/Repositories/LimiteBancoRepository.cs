using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ILimiteBancoRepository"/> usando EF Core + PostgreSQL.
/// </summary>
internal sealed class LimiteBancoRepository(SgcfDbContext context) : ILimiteBancoRepository
{
    public void Add(LimiteBanco limite) => context.LimitesBanco.Add(limite);

    public void Update(LimiteBanco limite) => context.LimitesBanco.Update(limite);

    public Task<LimiteBanco?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <summary>
    /// Retorna o limite vigente (sem data_vigencia_fim) para a combinação banco+modalidade.
    /// "Vigente" é definido como DataVigenciaFim == null (sem encerramento programado).
    /// </summary>
    public Task<LimiteBanco?> GetByBancoModalidadeAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId
                  && l.Modalidade == modalidade
                  && l.DataVigenciaFim == null,
                cancellationToken);

    public async Task<IReadOnlyList<LimiteBanco>> ListAsync(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LimiteBanco> q = context.LimitesBanco.AsNoTracking();

        if (bancoId.HasValue)
        {
            q = q.Where(l => l.BancoId == bancoId.Value);
        }

        if (modalidade.HasValue)
        {
            q = q.Where(l => l.Modalidade == modalidade.Value);
        }

        List<LimiteBanco> list = await q
            .OrderBy(l => l.BancoId)
            .ThenBy(l => l.Modalidade)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
