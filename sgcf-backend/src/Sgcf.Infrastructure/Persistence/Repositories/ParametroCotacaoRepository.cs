using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class ParametroCotacaoRepository(SgcfDbContext context) : IParametroCotacaoRepository
{
    public Task<ParametroCotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<ParametroCotacao>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ParametroCotacao>> ListAllAsync(CancellationToken cancellationToken)
    {
        List<ParametroCotacao> list = await context.Set<ParametroCotacao>().ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ParametroCotacao>> ListAtivosAsync(CancellationToken cancellationToken)
    {
        List<ParametroCotacao> list = await context.Set<ParametroCotacao>()
            .Where(p => p.Ativo)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public void Add(ParametroCotacao parametro) => context.Set<ParametroCotacao>().Add(parametro);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
