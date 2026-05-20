using Microsoft.Extensions.Hosting;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Api.HostedServices;

/// <summary>
/// Seed de tenant padrão "proxys" apenas em Development.
/// Garante que o dev-bypass JWT tenha um tenant válido no banco para o TenantResolverMiddleware.
/// </summary>
internal sealed class DevTenantSeederService(IServiceProvider sp) : IHostedService
{
    /// <summary>
    /// ID fixo do tenant Proxys em desenvolvimento.
    /// Deve ser o mesmo em todos os lugares: DevTenantSeeder, dev JWT mock e testes.
    /// </summary>
    public static readonly Guid ProxysTenantId = new("00000000-0000-7000-8000-000000000001");

    public async Task StartAsync(CancellationToken ct)
    {
        using IServiceScope scope = sp.CreateScope();
        ITenantRepository repo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (await repo.GetAsync(ProxysTenantId, ct) is not null)
        {
            return;
        }

        Tenant tenant = Tenant.Criar(
            ProxysTenantId,
            "proxys",
            "Proxys Comércio Eletrônico",
            "00000000000100",
            PlanoAssinatura.Padrao,
            clock);

        await repo.AddAsync(tenant, ct);
        await repo.SaveChangesAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
