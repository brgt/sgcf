using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Sgcf.Api.Middleware;
using Sgcf.Application;
using Sgcf.Application.Authorization;
using Sgcf.Infrastructure;

// QuestPDF community license — required before any PDF generation
QuestPDF.Settings.License = LicenseType.Community;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
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
    options.AddPolicy(Policies.Admin,     p => p.RequireRole("admin"));
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
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapControllers();

await app.RunAsync();
