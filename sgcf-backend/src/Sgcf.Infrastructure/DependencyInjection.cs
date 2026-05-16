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
using Sgcf.Infrastructure.Antecipacao;
using Sgcf.Infrastructure.Auditoria;
using Sgcf.Infrastructure.Caching;
using Sgcf.Infrastructure.Calendario;
using Sgcf.Infrastructure.Cambio;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;
using Sgcf.Infrastructure.Services;

namespace Sgcf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connStr = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionString 'Postgres' não configurada.");

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
        }
        else
        {
            services.AddDistributedMemoryCache();
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

        return services;
    }
}
