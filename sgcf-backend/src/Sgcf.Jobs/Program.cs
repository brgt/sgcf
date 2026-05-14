// Sgcf.Jobs -- Background jobs host entry point.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sgcf.Application;
using Sgcf.Infrastructure;
using Sgcf.Infrastructure.Bcb;
using Sgcf.Jobs.Jobs;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<BcbPtaxClient>()
    .AddStandardResilienceHandler();
builder.Services.AddScoped<PtaxIngestor>();
builder.Services.AddHostedService<IngestaoPtaxJob>();
builder.Services.AddHostedService<BackfillPtaxJob>();
builder.Services.AddHostedService<RecalcularMtmJob>();
builder.Services.AddHostedService<LiquidarNdfJob>();
builder.Services.AddHostedService<AlertaVencimentoJob>();
builder.Services.AddHostedService<AlertaExposicaoBancoJob>();
builder.Services.AddHostedService<SnapshotMensalJob>();
builder.Services.AddHostedService<ProvisaoJurosDiariaJob>();

IHost app = builder.Build();

await app.RunAsync();
