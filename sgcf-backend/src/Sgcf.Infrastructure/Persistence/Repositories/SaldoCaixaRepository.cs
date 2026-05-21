using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Tesouraria;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class SaldoCaixaRepository(SgcfDbContext context) : ISaldoCaixaRepository
{
    public Task<SaldoCaixa?> GetAsync(
        Guid contaId,
        LocalDate dataReferencia,
        CancellationToken ct = default) =>
        context.SaldosCaixa
            .FirstOrDefaultAsync(s => s.ContaId == contaId && s.DataReferencia == dataReferencia, ct);

    public async Task<IReadOnlyList<SaldoCaixa>> ListByContaAsync(
        Guid contaId,
        LocalDate dataDe,
        LocalDate dataAte,
        CancellationToken ct = default)
    {
        List<SaldoCaixa> list = await context.SaldosCaixa
            .Where(s => s.ContaId == contaId
                     && s.DataReferencia >= dataDe
                     && s.DataReferencia <= dataAte)
            .OrderByDescending(s => s.DataReferencia)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<SaldoCaixa>> ListByDataAsync(
        LocalDate dataReferencia,
        CancellationToken ct = default)
    {
        List<SaldoCaixa> list = await context.SaldosCaixa
            .Where(s => s.DataReferencia == dataReferencia)
            .ToListAsync(ct);

        return list.AsReadOnly();
    }

    public async Task AddAsync(SaldoCaixa saldo, CancellationToken ct = default)
    {
        await context.SaldosCaixa.AddAsync(saldo, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
