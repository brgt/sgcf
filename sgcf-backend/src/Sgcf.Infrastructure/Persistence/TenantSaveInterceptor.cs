using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Infrastructure.Persistence;

/// <summary>
/// EF Core save interceptor que preenche automaticamente <c>TenantId</c> em todas
/// as entidades <see cref="ITenantScoped"/> adicionadas no <c>SaveChanges</c>.
///
/// Regras de aplicação:
/// - EntityState.Added: popula TenantId a partir de <see cref="ITenantContext"/>.
/// - EntityState.Modified / Deleted: nenhuma ação — TenantId não muda após o INSERT.
/// - Contexto não resolvido (<see cref="ITenantContext.IsResolved"/> = false):
///   ignora silenciosamente para permitir seeds, migrations e jobs de sistema.
/// </summary>
internal sealed class TenantSaveInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PopularTenantId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PopularTenantId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void PopularTenantId(DbContext? context)
    {
        // Contexto não resolvido: jobs de sistema, seeds e migrations não têm tenant.
        if (context is null || !tenantContext.IsResolved)
        {
            return;
        }

        Guid tenantId = tenantContext.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            // Usa reflection via EF metadata para evitar cast para tipo concreto.
            // Alternativa de cast para setar via propriedade não funciona porque a
            // propriedade tem setter privado e o tipo concreto é desconhecido aqui.
            entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = tenantId;
        }
    }
}
