using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sgcf.Application;
using Sgcf.Application.Common;
using Sgcf.Infrastructure;
using Sgcf.Mcp.Services;
using Sgcf.Mcp.Tools;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (repositories, DbContext, Redis, IClock, ICotacaoSpotCache) ──
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application (MediatR handlers + FluentValidation pipeline) ────────────────
builder.Services.AddApplication();

// ── Audit context (MCP source overrides the system default) ───────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContextService, McpRequestContextService>();

// ── Authentication — JWT Bearer (same authority as Sgcf.Api) ─────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false
            };
        }
    });

builder.Services.AddAuthorization();

// ── MCP Server ────────────────────────────────────────────────────────────────
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "sgcf-mcp-server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithTools<ContratoTools>()
    .WithTools<DividaTools>()
    .WithTools<HedgeTools>()
    .WithTools<RagTools>();

// ── Health ────────────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapMcp("/mcp");

await app.RunAsync();
