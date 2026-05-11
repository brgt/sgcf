using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Painel;
using Sgcf.Domain.Painel;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class EbitdaMensalRepository(SgcfDbContext context) : IEbitdaMensalRepository
{
    public Task<EbitdaMensal?> GetAsync(int ano, int mes, CancellationToken cancellationToken) =>
        context.EbitdasMensais
            .FirstOrDefaultAsync(e => e.Ano == ano && e.Mes == mes, cancellationToken);

    public void Add(EbitdaMensal ebitda) => context.EbitdasMensais.Add(ebitda);

    public void Update(EbitdaMensal ebitda) => context.EbitdasMensais.Update(ebitda);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
