using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Painel;
using Sgcf.Domain.Painel;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class EbitdaMensalRepository(SgcfDbContext context) : IEbitdaMensalRepository
{
    public Task<EbitdaMensal?> GetAsync(int ano, int mes, CancellationToken cancellationToken) =>
        context.EbitdasMensais
            .FirstOrDefaultAsync(e => e.Ano == ano && e.Mes == mes, cancellationToken);

    public async Task<IReadOnlyList<EbitdaMensal>> ListUltimos12MesesAsync(
        int anoReferencia,
        int mesReferencia,
        CancellationToken cancellationToken)
    {
        // Calcula o primeiro mês da janela de 12 meses.
        // Ex: referência = 2026/05 → janela vai de 2025/06 a 2026/05.
        int anoInicio = mesReferencia == 12 ? anoReferencia : anoReferencia - 1;
        int mesInicio = mesReferencia == 12 ? 1 : mesReferencia + 1;

        // Compara usando (Ano * 100 + Mes) para evitar múltiplos predicados.
        int chaveInicio = anoInicio * 100 + mesInicio;
        int chaveFim = anoReferencia * 100 + mesReferencia;

        List<EbitdaMensal> resultado = await context.EbitdasMensais
            .Where(e => e.Ano * 100 + e.Mes >= chaveInicio
                     && e.Ano * 100 + e.Mes <= chaveFim)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return resultado.AsReadOnly();
    }

    public void Add(EbitdaMensal ebitda) => context.EbitdasMensais.Add(ebitda);

    public void Update(EbitdaMensal ebitda) => context.EbitdasMensais.Update(ebitda);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
