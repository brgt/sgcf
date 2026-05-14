using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Contabilidade;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class LancamentoContabilRepository(SgcfDbContext context) : ILancamentoContabilRepository
{
    public Task<bool> ExisteAsync(Guid contratoId, LocalDate data, string origem, CancellationToken ct) =>
        context.LancamentosContabeis.AnyAsync(
            l => l.ContratoId == contratoId && l.Data == data && l.Origem == origem,
            ct);

    public async Task<IReadOnlyList<LancamentoContabil>> ListByContratoAsync(Guid contratoId, CancellationToken ct) =>
        await context.LancamentosContabeis
            .Where(l => l.ContratoId == contratoId)
            .OrderByDescending(l => l.Data)
            .ToListAsync(ct);

    public void Add(LancamentoContabil lancamento) => context.LancamentosContabeis.Add(lancamento);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
