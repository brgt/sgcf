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

    public void Add(LancamentoContabil lancamento) => context.LancamentosContabeis.Add(lancamento);

    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
