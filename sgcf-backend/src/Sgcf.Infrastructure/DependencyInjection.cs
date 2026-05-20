using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Sgcf.Application.Alertas;
using Sgcf.Application.Antecipacao;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Bancos;
using Sgcf.Application.Calendario;
using Sgcf.Application.Common;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Hedge;
using Sgcf.Application.Painel;
using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Cache;
using Sgcf.Application.Sistema;
using Sgcf.Application.Tenancy;
using Sgcf.Infrastructure.Antecipacao;
using Sgcf.Infrastructure.Cache.Simulacao;
using Sgcf.Infrastructure.Auditoria;
using Sgcf.Infrastructure.Caching;
using Sgcf.Infrastructure.Calendario;
using Sgcf.Infrastructure.Cambio;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Sgcf.Infrastructure.Services;
using Sgcf.Infrastructure.Tenancy;

namespace Sgcf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connStr = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionString 'Postgres' não configurada.");

        // TenantContext: scoped por request — resolvido pelo TenantResolverMiddleware.
        // Registrado em dois formatos: concreto (para Resolve interno) e interface (para Application).
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<AuditInterceptor>();
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
        services.AddScoped<IRequestContextService, SystemRequestContextService>();

        services.AddDbContext<SgcfDbContext>((sp, options) =>
            options
                .UseNpgsql(connStr, npgsql => npgsql.UseNodaTime())
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        string? redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
        {
            services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);

            // IConnectionMultiplexer: necessário para RedisCronogramaSimulacaoCache,
            // que usa a API de baixo nível (SET com TTL + índice via SADD) em vez de IDistributedCache.
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                _ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));

            // Cache Redis para cronograma hipotético de simulação (AD-3)
            services.Configure<CronogramaSimulacaoCacheOptions>(
                configuration.GetSection("CronogramaSimulacaoCache"));
            services.AddSingleton<ICronogramaSimulacaoCache, RedisCronogramaSimulacaoCache>();
        }
        else
        {
            services.AddDistributedMemoryCache();

            // Fallback explícito quando Redis não está disponível: cache no-op.
            // Necessário porque os handlers de Simulação/Painel injetam ICronogramaSimulacaoCache
            // diretamente (sem Service Locator). Sem essa linha o DI falha no startup.
            services.AddSingleton<ICronogramaSimulacaoCache, NullCronogramaSimulacaoCache>();
        }

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddScoped<IBancoRepository, BancoRepository>();
        services.AddScoped<IContratoRepository, ContratoRepository>();
        services.AddScoped<IGarantiaRepository, GarantiaRepository>();
        services.AddScoped<IPlanoContasRepository, PlanoContasRepository>();
        services.AddScoped<IParametroCotacaoRepository, ParametroCotacaoRepository>();
        services.AddScoped<IEventoCronogramaRepository, EventoCronogramaRepository>();
        services.AddScoped<ICotacaoFxRepository, CotacaoFxRepository>();
        services.AddScoped<ICotacaoSpotCache, RedisCotacaoSpotCache>();
        services.AddScoped<IResolveTipoCotacaoService, CotacaoResolverService>();
        services.AddScoped<ISimulacaoAntecipacaoRepository, SimulacaoAntecipacaoRepository>();
        services.AddScoped<IHedgeRepository, HedgeRepository>();
        services.AddScoped<IEbitdaMensalRepository, EbitdaMensalRepository>();
        services.AddScoped<IExportacaoAuditLog, ExportacaoAuditLog>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAlertaVencimentoRepository, AlertaVencimentoRepository>();
        services.AddScoped<IAlertaExposicaoBancoRepository, AlertaExposicaoBancoRepository>();
        services.AddScoped<ISnapshotMensalPosicaoRepository, SnapshotMensalPosicaoRepository>();
        services.AddScoped<ILancamentoContabilRepository, LancamentoContabilRepository>();
        services.AddScoped<IFeriadoRepository, FeriadoRepository>();
        services.AddScoped<IBusinessDayCalendar, BusinessDayCalendar>();
        services.AddScoped<ICotacaoRepository, CotacaoRepository>();
        services.AddScoped<ILimiteBancoRepository, LimiteBancoRepository>();
        services.AddScoped<IEconomiaRepository, EconomiaRepository>();
        services.AddScoped<ICdiSnapshotRepository, CdiSnapshotRepository>();
        services.AddScoped<ICenarioSimulacaoRepository, CenarioSimulacaoRepository>();
        services.AddScoped<IParametroSistemaRepository, ParametroSistemaRepository>();

        return services;
    }
}
