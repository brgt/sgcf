using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Sgcf.Api.Middleware;
using Sgcf.Api.Services;
using Sgcf.Application;
using Sgcf.Application.Authorization;
using Sgcf.Application.Common;
using Sgcf.Infrastructure;

// QuestPDF community license — required before any PDF generation
QuestPDF.Settings.License = LicenseType.Community;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Security guard: dev-bypass NÃO pode escapar para fora de localhost ────────
//
// O bloco JwtBearer abaixo (linhas ~61-97) aceita qualquer Bearer token em
// Development e injeta um principal com todos os roles (admin+tesouraria+...).
// Se ASPNETCORE_ENVIRONMENT=Development vazar para produção/staging, qualquer
// token concede acesso total — comprometimento completo.
//
// Este guard falha na inicialização quando:
//   1. O ambiente é Development, E
//   2. Auth:Authority não está configurado (indica ausência de IdP real), E
//   3. O hostname não parece localhost/dev-container.
//
// Em produção, Auth:Authority deve sempre estar configurado, então a condição
// (2) nunca se satisfaz e o guard não interfere.
if (builder.Environment.IsDevelopment())
{
    string hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    string? authority = builder.Configuration["Auth:Authority"];

    bool ehAmbienteLocal =
        hostname is "localhost" or "127.0.0.1" ||
        hostname.StartsWith("dev-",    StringComparison.OrdinalIgnoreCase) ||
        hostname.Contains("docker",    StringComparison.OrdinalIgnoreCase) ||
        hostname.Contains("container", StringComparison.OrdinalIgnoreCase) ||
        // Prefixo padrão de nomes de containers Docker em Compose (ex: sgcf-api-1)
        hostname.Contains("sgcf",      StringComparison.OrdinalIgnoreCase);

    if (!ehAmbienteLocal && string.IsNullOrEmpty(authority))
    {
        throw new InvalidOperationException(
            "SECURITY: ASPNETCORE_ENVIRONMENT=Development sem Auth:Authority configurado " +
            $"em host '{hostname}'. Recusa inicialização para evitar exposição do dev-bypass JWT. " +
            "Defina Auth:Authority em produção/staging ou execute em localhost/dev-container.");
    }
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddScoped<IRequestContextService, HttpRequestContextService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "JWT Bearer token. Example: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience  = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = false,
                ValidateAudience         = false,
                ValidateIssuerSigningKey = false,
                ValidateLifetime         = false,
                SignatureValidator       = (_, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken("eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.e30."),
            };

            // Accept any Bearer token in dev — create a fully-privileged dev principal
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var claims = new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "dev-user"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-user-id"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "tesouraria"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "gerente"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "diretor"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "contabilidade"),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "auditor"),
                        };
                        ctx.Principal = new System.Security.Claims.ClaimsPrincipal(
                            new System.Security.Claims.ClaimsIdentity(claims, "DevMock"));
                        ctx.Success();
                    }
                    return Task.CompletedTask;
                },
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.Leitura,   p => p.RequireAuthenticatedUser());
    options.AddPolicy(Policies.Escrita,   p => p.RequireRole("tesouraria", "admin"));
    options.AddPolicy(Policies.Gerencial, p => p.RequireRole("gerente", "diretor", "admin"));
    options.AddPolicy(Policies.Executivo, p => p.RequireRole("tesouraria", "gerente", "diretor", "admin"));
    options.AddPolicy(Policies.Auditoria, p => p.RequireRole("contabilidade", "auditor", "admin"));
    options.AddPolicy(Policies.Admin,      p => p.RequireRole("admin"));
    options.AddPolicy(Policies.SuperAdmin, p => p.RequireRole("super-admin"));
});

builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://localhost:4173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Sgcf.Api.Filters.IdempotencyFilter>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Aviso explícito no boot para que nenhum dev suba um container com
    // dev-bypass ativo sem perceber no log. Usa LoggerMessage (CA1848).
    SecurityLogs.DevBypassAtivo(app.Logger);
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapControllers();

await app.RunAsync();

// Expõe Program para WebApplicationFactory nos testes de integração
public partial class Program { }

/// <summary>
/// Mensagens de log de segurança em formato LoggerMessage (CA1848 — delegates pré-compilados
/// evitam boxing de parâmetros no hot path de log).
/// </summary>
internal static partial class SecurityLogs
{
    [LoggerMessage(
        EventId = 9001,
        Level   = LogLevel.Warning,
        Message = "SECURITY WARNING: dev-bypass JWT está ativo. " +
                  "Qualquer Bearer token concede todos os roles " +
                  "(admin, tesouraria, gerente, diretor, contabilidade, auditor). " +
                  "NUNCA executar em produção ou staging — somente localhost/dev-container.")]
    public static partial void DevBypassAtivo(ILogger logger);
}
