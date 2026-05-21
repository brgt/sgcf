using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Alertas.Rules;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;
using Sgcf.Infrastructure.Tenancy;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Hosted service que avalia todas as <see cref="IAlertaRule"/> registradas
/// para cada tenant ativo, a cada 5 minutos.
///
/// Fluxo por ciclo:
/// 1. Cria um escopo raiz para ler a lista de tenants ativos (tabela Tenants não é tenant-scoped).
/// 2. Para cada tenant ativo: cria um escopo filho, resolve <see cref="ITenantContext"/> e
///    executa cada regra sequencialmente.
/// 3. Erros em um tenant não interrompem os demais — apenas o erro é logado.
///
/// A idempotência é garantida pelas próprias regras via <see cref="IAlertaRepository.TryAddIdempotentAsync"/>.
/// </summary>
internal sealed partial class AlertasHostedService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<AlertasHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AlertasHostedService: iniciando ciclo para {Hoje} — {TotalTenants} tenant(s) ativo(s).")]
    private static partial void LogIniciandoCiclo(ILogger logger, LocalDate hoje, int totalTenants);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AlertasHostedService: tenant {TenantSlug} — regra '{RegraName}' concluída.")]
    private static partial void LogRegraConcluida(ILogger logger, string tenantSlug, string regraName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "AlertasHostedService: erro na regra '{RegraName}' para tenant {TenantSlug}: {Mensagem}")]
    private static partial void LogErroRegra(ILogger logger, string regraName, string tenantSlug, string mensagem, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "AlertasHostedService: erro inesperado no ciclo: {Mensagem}")]
    private static partial void LogErroCiclo(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AvaliarTodasAsRegrasAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroCiclo(logger, ex.Message, ex);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task AvaliarTodasAsRegrasAsync(CancellationToken ct)
    {
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;

        // Escopo raiz para ler tenants — Tenants não é tenant-scoped, sem necessidade de resolver contexto.
        IReadOnlyList<Tenant> tenantsAtivos;
        using (IServiceScope rootScope = scopeFactory.CreateScope())
        {
            ITenantRepository tenantRepo = rootScope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var pagina = await tenantRepo.ListAsync(StatusTenant.Ativo, page: 1, pageSize: 1000, ct: ct);
            tenantsAtivos = pagina.Items;
        }

        LogIniciandoCiclo(logger, hoje, tenantsAtivos.Count);

        foreach (Tenant tenant in tenantsAtivos)
        {
            await AvaliarTenantAsync(tenant, hoje, ct);
        }
    }

    /// <summary>
    /// Avalia todas as regras para um único tenant.
    /// Cria um escopo dedicado e resolve o <see cref="ITenantContext"/> antes de executar as regras,
    /// de modo que o global query filter do EF Core veja apenas as linhas desse tenant.
    /// </summary>
    private async Task AvaliarTenantAsync(Tenant tenant, LocalDate hoje, CancellationToken ct)
    {
        using IServiceScope tenantScope = scopeFactory.CreateScope();
        IServiceProvider sp = tenantScope.ServiceProvider;

        // Resolve o contexto de tenant para este escopo — ativa o global query filter do EF Core.
        TenantContext tenantContext = sp.GetRequiredService<TenantContext>();
        tenantContext.Resolve(
            tenantId: tenant.Id,
            slug: tenant.Slug,
            isSuperAdmin: false,
            isImpersonating: false);

        IEnumerable<IAlertaRule> regras = sp.GetServices<IAlertaRule>();

        foreach (IAlertaRule regra in regras)
        {
            try
            {
                await regra.AvaliarAsync(hoje, ct);
                LogRegraConcluida(logger, tenant.Slug, regra.Nome);
                SgcfJobsMetrics.AlertaRuleAvaliada.Add(1);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroRegra(logger, regra.Nome, tenant.Slug, ex.Message, ex);
            }
        }
    }
}
