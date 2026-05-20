using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Api.IntegrationTests.TestAuth;

/// <summary>
/// Deve corresponder exatamente a <c>DevTenantSeederService.ProxysTenantId</c> em Sgcf.Api.
/// Duplicado aqui porque DevTenantSeederService é internal — mudanças lá devem ser espelhadas.
/// </summary>
internal static class ProxysDevTenant
{
    public static readonly Guid Id = new("00000000-0000-7000-8000-000000000001");
    public const string Slug   = "proxys";
    public const string Nome   = "Proxys Comércio Eletrônico";
    public const string Cnpj   = "00000000000100";
}

/// <summary>
/// Semeia o tenant Proxys no banco de testes após <c>MigrateAsync()</c>.
///
/// <c>DevTenantSeederService</c> roda no startup do host e falha com 42P01 porque
/// as migrations ainda não foram aplicadas. Este helper cobre o seed pós-migration
/// para que <c>TenantResolverMiddleware</c> encontre o tenant e não retorne 401.
/// </summary>
internal static class TenantTestSeeder
{
    public static async Task SeedProxysAsync(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        ITenantRepository repo  = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        IClock            clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (await repo.GetAsync(ProxysDevTenant.Id, CancellationToken.None) is not null)
        {
            return;
        }

        Tenant tenant = Tenant.Criar(
            ProxysDevTenant.Id,
            ProxysDevTenant.Slug,
            ProxysDevTenant.Nome,
            ProxysDevTenant.Cnpj,
            PlanoAssinatura.Padrao,
            clock);

        await repo.AddAsync(tenant, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);
    }
}
