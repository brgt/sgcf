using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Sistema;
using Sgcf.Domain.Sistema;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IParametroSistemaRepository"/>.
///
/// Mantém o singleton via get-or-create: tenta buscar a linha "GLOBAL" e,
/// se não existir, cria e persiste automaticamente. Isso garante que ambientes
/// novos (ex: containers de teste) funcionem sem seed manual.
///
/// Concorrência: em cenários de alta concorrência, duas transações podem tentar
/// inserir ao mesmo tempo. O índice único em <c>chave</c> garante que apenas uma
/// terá sucesso — a outra receberá uma exception de constraint. Para o MVP,
/// este padrão é suficiente (baixa frequência de escrita em parâmetros de sistema).
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
        await context.SaveChangesAsync(ct);

        return novo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
