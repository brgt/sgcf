using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contabilidade;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class DadosContabeisRepository(SgcfDbContext context) : IDadosContabeisRepository
{
    public Task<DadosContabeisMensal?> GetByCompetenciaAsync(int ano, int mes, CancellationToken ct) =>
        context.DadosContabeisMensais
            .FirstOrDefaultAsync(e => e.Ano == ano && e.Mes == mes, ct);

    public async Task<IReadOnlyList<DadosContabeisMensal>> ListUltimos12MesesAsync(
        int anoReferencia,
        int mesReferencia,
        CancellationToken ct)
    {
        // Janela: últimos 12 meses até o mês de referência (inclusive).
        // Ex: referência = 2026/05 → janela 2025/06 a 2026/05.
        int anoInicio = mesReferencia == 12 ? anoReferencia : anoReferencia - 1;
        int mesInicio = mesReferencia == 12 ? 1 : mesReferencia + 1;

        int chaveInicio = anoInicio * 100 + mesInicio;
        int chaveFim = anoReferencia * 100 + mesReferencia;

        List<DadosContabeisMensal> resultado = await context.DadosContabeisMensais
            .Where(e => e.Ano * 100 + e.Mes >= chaveInicio
                     && e.Ano * 100 + e.Mes <= chaveFim)
            .AsNoTracking()
            .ToListAsync(ct);

        return resultado.AsReadOnly();
    }

    public void Add(DadosContabeisMensal dados) => context.DadosContabeisMensais.Add(dados);

    public void Update(DadosContabeisMensal dados) => context.DadosContabeisMensais.Update(dados);

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
