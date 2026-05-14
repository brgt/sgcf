using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Sgcf.A2a.Protocol;
using Sgcf.A2a.Services;
using Sgcf.Application;
using Sgcf.Application.Common;
using Sgcf.Application.Painel.Queries;
using Sgcf.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Infrastructure + Application ─────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// ── Audit context (A2A source overrides the system default) ───────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContextService, A2aRequestContextService>();

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false
            };
        }
    });

builder.Services.AddAuthorization();

// ── In-memory task store (MVP) ────────────────────────────────────────────────
builder.Services.AddSingleton<A2aTaskStore>();

builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

JsonSerializerOptions jsonOpts = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
};

// =============================================================================
// GET /.well-known/agent.json — A2A Agent Card
// =============================================================================
app.MapGet("/.well-known/agent.json", (IConfiguration config) =>
{
    string baseUrl = config["A2a:BaseUrl"] ?? "http://localhost:5001";

    // CA1861: arrays extracted to local variables to avoid repeated inline allocation.
    string[] defaultInputModes = ["text/plain"];
    string[] defaultOutputModes = ["application/json"];
    string[] bearerSchemes = ["Bearer"];
    string[] skillTags = ["dívida", "financeiro", "tesouraria", "hedge", "câmbio"];
    string[] skillExamples =
    [
        "Qual é a posição de dívida hoje?",
        "Mostre o saldo total por moeda",
        "Qual o impacto de uma alta de 10% no dólar na dívida?"
    ];
    string[] skillInputModes = ["text/plain"];
    string[] skillOutputModes = ["application/json"];

    AgentCard card = new(
        SchemaVersion: "0.2",
        Name: "SGCF — Sistema de Gestão de Contratos de Financiamento",
        Description: "Agente de consulta de dívida corporativa da Proxys. Fornece posição consolidada de dívida em múltiplas moedas, MTM de hedges, simulação de cenários cambiais e calendário de vencimentos.",
        Url: baseUrl,
        Version: "1.0.0",
        Provider: new AgentProvider(
            Organization: "Proxys Comércio Eletrônico",
            Url: "https://proxysgroup.com.br"
        ),
        Capabilities: new AgentCapabilities(
            Streaming: false,
            PushNotifications: false,
            StateTransitionHistory: false
        ),
        Authentication: new AgentAuth(Schemes: bearerSchemes),
        DefaultInputModes: defaultInputModes,
        DefaultOutputModes: defaultOutputModes,
        Skills:
        [
            new AgentSkill(
                Id: "consulta_posicao_divida",
                Name: "Consulta Posição de Dívida",
                Description: "Retorna a posição consolidada de dívida em múltiplas moedas com ajuste MTM de hedges e alertas de exposição.",
                Tags: skillTags,
                Examples: skillExamples,
                InputModes: skillInputModes,
                OutputModes: skillOutputModes
            )
        ]
    );

    return Results.Json(card, jsonOpts);
});

// =============================================================================
// POST /a2a/tasks — Submit a new task
// =============================================================================
app.MapPost("/a2a/tasks", async (
    HttpContext ctx,
    A2aTaskStore store,
    IMediator mediator) =>
{
    A2aTaskRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<A2aTaskRequest>(jsonOpts);
    }
    catch
    {
        return Results.BadRequest(new { error = "JSON inválido." });
    }

    if (request is null || string.IsNullOrWhiteSpace(request.Id))
    {
        return Results.BadRequest(new { error = "Campo 'id' é obrigatório." });
    }

    string resultJson;
    try
    {
        Sgcf.Application.Painel.PainelDividaDto posicao = await mediator.Send(new GetPainelDividaQuery(), ctx.RequestAborted);
        resultJson = JsonSerializer.Serialize(posicao, jsonOpts);
    }
    catch (Exception ex)
    {
        A2aTask failedTask = new(
            Id: request.Id,
            Status: new A2aTaskStatus(State: "failed"),
            Result: new A2aMessage(
                Role: "agent",
                Parts: [new A2aPart(Type: "text", Text: $"Erro ao processar consulta: {ex.Message}")]
            )
        );
        store.Upsert(failedTask);
        return Results.Json(failedTask, jsonOpts, statusCode: 200);
    }

    A2aTask completedTask = new(
        Id: request.Id,
        Status: new A2aTaskStatus(State: "completed"),
        Result: new A2aMessage(
            Role: "agent",
            Parts: [new A2aPart(Type: "text", Text: resultJson)]
        )
    );

    store.Upsert(completedTask);
    return Results.Json(completedTask, jsonOpts, statusCode: 200);
});

// =============================================================================
// GET /a2a/tasks/{id} — Poll task status
// =============================================================================
app.MapGet("/a2a/tasks/{id}", (string id, A2aTaskStore store) =>
{
    A2aTask? task = store.Get(id);
    return task is null
        ? Results.NotFound(new { error = $"Task '{id}' não encontrada." })
        : Results.Json(task, jsonOpts);
});

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.RunAsync();
