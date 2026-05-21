using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Api.HostedServices;

/// <summary>
/// Seed de tenant padrão "proxys" apenas em Development.
/// Garante que o dev-bypass JWT tenha um tenant válido no banco para o TenantResolverMiddleware.
/// </summary>
internal sealed partial class DevTenantSeederService(
    IServiceProvider sp,
    ILogger<DevTenantSeederService> logger) : IHostedService
{
    /// <summary>
    /// ID fixo do tenant Proxys em desenvolvimento.
    /// Deve ser o mesmo em todos os lugares: DevTenantSeeder, dev JWT mock e testes.
    /// </summary>
    public static readonly Guid ProxysTenantId = new("00000000-0000-7000-8000-000000000001");

    public async Task StartAsync(CancellationToken ct)
    {
        try
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
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Migrations ainda não foram aplicadas (comum em testes de integração onde
            // MigrateAsync() é chamado DEPOIS do host iniciar). Seed adiado para próxima execução.
            DevSeedIgnorado(logger);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning,
        Message = "DevTenantSeeder: tabela 'sgcf.tenant' não existe ainda — seed ignorado (migrations pendentes).")]
    private static partial void DevSeedIgnorado(ILogger logger);
}
