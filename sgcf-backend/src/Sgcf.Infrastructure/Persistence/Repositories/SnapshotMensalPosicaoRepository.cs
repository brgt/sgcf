using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Painel;
using Sgcf.Domain.Painel;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class SnapshotMensalPosicaoRepository(SgcfDbContext context) : ISnapshotMensalPosicaoRepository
{
    public Task<bool> ExisteParaMesAsync(int ano, int mes, CancellationToken ct) =>
        context.SnapshotsMensais.AnyAsync(s => s.Ano == ano && s.Mes == mes, ct);

    public void Add(SnapshotMensalPosicao snap) => context.SnapshotsMensais.Add(snap);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
