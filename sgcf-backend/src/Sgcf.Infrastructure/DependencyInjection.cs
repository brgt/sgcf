using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Sgcf.Application.Antecipacao;
using Sgcf.Application.Auditoria;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Hedge;
using Sgcf.Application.Painel;
using Sgcf.Infrastructure.Antecipacao;
using Sgcf.Infrastructure.Auditoria;
using Sgcf.Infrastructure.Caching;
using Sgcf.Infrastructure.Cotacoes;
using Sgcf.Infrastructure.Cronograma;
using Sgcf.Infrastructure.Persistence;
using Sgcf.Infrastructure.Persistence.Repositories;

namespace Sgcf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connStr = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionString 'Postgres' não configurada.");

        services.AddDbContext<SgcfDbContext>(options =>
            options.UseNpgsql(connStr, npgsql => npgsql.UseNodaTime()));

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
        services.AddScoped<IGeradorCronograma, BulletGeradorStrategy>();
        services.AddScoped<IGerarSacStrategy, SacGeradorStrategy>();
        services.AddScoped<ICotacaoSpotCache, RedisCotacaoSpotCache>();
        services.AddScoped<IResolveTipoCotacaoService, CotacaoResolverService>();
        services.AddScoped<ISimulacaoAntecipacaoRepository, SimulacaoAntecipacaoRepository>();
        services.AddScoped<IHedgeRepository, HedgeRepository>();
        services.AddScoped<IEbitdaMensalRepository, EbitdaMensalRepository>();
        services.AddScoped<IExportacaoAuditLog, ExportacaoAuditLog>();

        return services;
    }
}
