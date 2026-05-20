using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Sistema;
using Sgcf.Domain.Sistema;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IParametroSistemaRepository"/>.
///
/// Mantém o singleton via get-or-create com proteção a race condition:
/// <list type="bullet">
///   <item>Tenta ler a linha "GLOBAL"; retorna imediatamente se existir.</item>
///   <item>Se não existir, tenta inserir.</item>
///   <item>
///     Em caso de <see cref="DbUpdateException"/> por violação do índice único em
///     <c>chave</c> (race: outra request inseriu primeiro), faz um segundo SELECT
///     e retorna a linha vencedora. Isso garante que <b>sempre</b> retorna exatamente
///     uma instância sem duplicatas — sem SQL raw, sem lock pessimista.
///   </item>
/// </list>
/// </summary>
internal sealed class ParametroSistemaRepository(SgcfDbContext context) : IParametroSistemaRepository
{
    public async Task<ParametroSistema> GetOrCreateGlobalAsync(IClock clock, CancellationToken ct = default)
    {
        ParametroSistema? existente = await context.Set<ParametroSistema>()
            .FirstOrDefaultAsync(p => p.Chave == ParametroSistema.ChaveGlobal, ct);

        if (existente is not null)
        {
            return existente;
        }

        ParametroSistema novo = ParametroSistema.Criar(clock);
        context.Set<ParametroSistema>().Add(novo);

        try
        {
            await context.SaveChangesAsync(ct);
            return novo;
        }
        catch (DbUpdateException)
        {
            // Race condition: outra request inseriu a linha entre o SELECT e o INSERT.
            // Descarta o estado inválido do ChangeTracker e relê a linha vencedora.
            context.ChangeTracker.Clear();

            return await context.Set<ParametroSistema>()
                .FirstAsync(p => p.Chave == ParametroSistema.ChaveGlobal, ct);
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
