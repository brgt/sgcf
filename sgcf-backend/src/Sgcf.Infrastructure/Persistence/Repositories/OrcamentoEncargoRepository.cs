using Microsoft.EntityFrameworkCore;
using Sgcf.Application.OrcamentosEncargo;
using Sgcf.Domain.OrcamentosEncargo;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class OrcamentoEncargoRepository(SgcfDbContext context) : IOrcamentoEncargoRepository
{
    public Task<OrcamentoEncargo?> GetAsync(
        int ano,
        int mes,
        string tipoEncargo,
        Guid? bancoId,
        Guid? contratoId,
        CancellationToken ct) =>
        context.OrcamentosEncargo
            .FirstOrDefaultAsync(e =>
                e.Ano == ano &&
                e.Mes == mes &&
                e.TipoEncargo == tipoEncargo &&
                e.BancoId == bancoId &&
                e.ContratoId == contratoId,
                ct);

    public async Task<IReadOnlyList<OrcamentoEncargo>> ListAsync(
        int deAno,
        int deMes,
        int ateAno,
        int ateMes,
        Guid? bancoId,
        string? tipoEncargo,
        CancellationToken ct)
    {
        int chaveInicio = deAno * 100 + deMes;
        int chaveFim = ateAno * 100 + ateMes;

        IQueryable<OrcamentoEncargo> query = context.OrcamentosEncargo
            .Where(e => e.Ano * 100 + e.Mes >= chaveInicio
                     && e.Ano * 100 + e.Mes <= chaveFim);

        if (bancoId.HasValue)
        {
            query = query.Where(e => e.BancoId == bancoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tipoEncargo))
        {
            query = query.Where(e => e.TipoEncargo == tipoEncargo);
        }

        List<OrcamentoEncargo> resultado = await query
            .AsNoTracking()
            .ToListAsync(ct);

        return resultado.AsReadOnly();
    }

    public void Add(OrcamentoEncargo orcamento) =>
        context.OrcamentosEncargo.Add(orcamento);

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
