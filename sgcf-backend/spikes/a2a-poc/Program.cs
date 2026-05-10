// =============================================================================
// SPIKE: A2A Agent — SGCF POC
// Proves .NET 10/11 can serve a valid A2A Agent Card and handle task execution.
//
// No official Google A2A .NET SDK exists (as of 2026-05-10).
// Protocol is implemented manually from the A2A spec v0.2:
//   https://google.github.io/A2A/
//
// Endpoints:
//   GET  /.well-known/agent.json   — Agent Card (discovery)
//   POST /a2a/tasks                — Submit a new task (synchronous, no streaming)
//   GET  /a2a/tasks/{id}           — Poll task status / result
//
// NOT production-ready. Missing: auth, streaming, durable state, real handlers.
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SgcfA2aPoc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

// In-memory task store — replace with Postgres a2a_tasks table in production
builder.Services.AddSingleton<TaskStore>();

var app = builder.Build();

// ── JSON serialization options ────────────────────────────────────────────────
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented               = true
};

// =============================================================================
// GET /.well-known/agent.json  — Agent Card
// A2A discovery endpoint. Clients fetch this to learn the agent's capabilities.
// =============================================================================
app.MapGet("/.well-known/agent.json", () =>
{
    var card = new AgentCard(
        SchemaVersion: "0.2",
        Name:          "SGCF — Sistema de Gestão de Contratos da Proxys",
        Description:   "Agente de consulta de dívida corporativa. Fornece posição consolidada, MTM de hedges, simulação de cenários cambiais e busca em contratos.",
        Url:           "http://localhost:5001",
        Version:       "0.1.0-spike",
        Provider: new AgentProvider(
            Organization: "Proxys Comércio Eletrônico",
            Url:          "https://proxysgroup.com.br"
        ),
        Capabilities: new AgentCapabilities(
            Streaming:              false,
            PushNotifications:      false,
            StateTransitionHistory: false
        ),
        Authentication:     new AgentAuth(Schemes: ["Bearer"]),
        DefaultInputModes:  ["text/plain"],
        DefaultOutputModes: ["application/json"],
        Skills:
        [
            new AgentSkill(
                Id:          "consulta_posicao_divida",
                Name:        "Consulta Posição de Dívida",
                Description: "Retorna a posição consolidada de dívida em múltiplas moedas com ajuste MTM de hedges.",
                Tags:        ["dívida", "financeiro", "tesouraria"],
                Examples:    ["Qual é a posição de dívida hoje?", "Mostre o saldo total por moeda"],
                InputModes:  ["text/plain"],
                OutputModes: ["application/json"]
            )
        ]
    );

    return Results.Json(card, jsonOptions);
});

// =============================================================================
// POST /a2a/tasks  — Submit a new task
// Body: A2aTaskRequest  { id, message: { role, parts: [{ text }] } }
// Returns the completed task synchronously (no streaming in this spike).
// =============================================================================
app.MapPost("/a2a/tasks", async (HttpContext ctx, TaskStore store) =>
{
    A2aTaskRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<A2aTaskRequest>(jsonOptions);
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body." });
    }

    if (request is null || string.IsNullOrWhiteSpace(request.Id))
        return Results.BadRequest(new { error = "Field 'id' is required." });

    // Extract plain-text input from the message parts
    var userText = request.Message?.Parts?.FirstOrDefault()?.Text ?? "(empty)";

    // ── Dispatch to skill (hardcoded for spike) ──────────────────────────────
    // In production: inspect the text, resolve to a skill, dispatch via IMediator
    var resultJson = DummySkillHandler.Execute(userText);

    var task = new A2aTask(
        Id:     request.Id,
        Status: new A2aTaskStatus(State: "completed"),
        Result: new A2aMessage(
            Role:  "agent",
            Parts: [new A2aPart(Text: resultJson)]
        )
    );

    store.Upsert(task);
    return Results.Json(task, jsonOptions, statusCode: 200);
});

// =============================================================================
// GET /a2a/tasks/{id}  — Poll task status
// =============================================================================
app.MapGet("/a2a/tasks/{id}", (string id, TaskStore store) =>
{
    var task = store.Get(id);
    return task is null
        ? Results.NotFound(new { error = $"Task '{id}' not found." })
        : Results.Json(task, jsonOptions);
});

// ── Health check ──────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run("http://localhost:5001");


// =============================================================================
// Domain models — A2A protocol shapes (spec v0.2)
// =============================================================================
namespace SgcfA2aPoc
{
    // ── Agent Card ────────────────────────────────────────────────────────────
    record AgentCard(
        [property: JsonPropertyName("schemaVersion")]   string SchemaVersion,
        [property: JsonPropertyName("name")]            string Name,
        [property: JsonPropertyName("description")]     string Description,
        [property: JsonPropertyName("url")]             string Url,
        [property: JsonPropertyName("version")]         string Version,
        [property: JsonPropertyName("provider")]        AgentProvider Provider,
        [property: JsonPropertyName("capabilities")]    AgentCapabilities Capabilities,
        [property: JsonPropertyName("authentication")]  AgentAuth Authentication,
        [property: JsonPropertyName("defaultInputModes")]  string[] DefaultInputModes,
        [property: JsonPropertyName("defaultOutputModes")] string[] DefaultOutputModes,
        [property: JsonPropertyName("skills")]          AgentSkill[] Skills
    );

    record AgentProvider(
        [property: JsonPropertyName("organization")] string Organization,
        [property: JsonPropertyName("url")]          string Url
    );

    record AgentCapabilities(
        [property: JsonPropertyName("streaming")]              bool Streaming,
        [property: JsonPropertyName("pushNotifications")]      bool PushNotifications,
        [property: JsonPropertyName("stateTransitionHistory")] bool StateTransitionHistory
    );

    record AgentAuth(
        [property: JsonPropertyName("schemes")] string[] Schemes
    );

    record AgentSkill(
        [property: JsonPropertyName("id")]          string Id,
        [property: JsonPropertyName("name")]        string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("tags")]        string[] Tags,
        [property: JsonPropertyName("examples")]    string[] Examples,
        [property: JsonPropertyName("inputModes")]  string[] InputModes,
        [property: JsonPropertyName("outputModes")] string[] OutputModes
    );

    // ── Task request / response ───────────────────────────────────────────────
    record A2aTaskRequest(
        [property: JsonPropertyName("id")]      string? Id,
        [property: JsonPropertyName("message")] A2aMessage? Message
    );

    record A2aTask(
        [property: JsonPropertyName("id")]     string Id,
        [property: JsonPropertyName("status")] A2aTaskStatus Status,
        [property: JsonPropertyName("result")] A2aMessage? Result
    );

    record A2aTaskStatus(
        [property: JsonPropertyName("state")] string State   // "submitted" | "working" | "completed" | "failed"
    );

    record A2aMessage(
        [property: JsonPropertyName("role")]  string Role,   // "user" | "agent"
        [property: JsonPropertyName("parts")] A2aPart[] Parts
    );

    record A2aPart(
        [property: JsonPropertyName("text")] string? Text
    );

    // ── In-memory task store ──────────────────────────────────────────────────
    sealed class TaskStore
    {
        private readonly ConcurrentDictionary<string, A2aTask> _tasks = new();
        public void Upsert(A2aTask task)  => _tasks[task.Id] = task;
        public A2aTask? Get(string id)    => _tasks.TryGetValue(id, out var t) ? t : null;
    }

    // ── Dummy skill handler ───────────────────────────────────────────────────
    // Placeholder for real IMediator.Send(new GetPosicaoDividaQuery(...)) call
    static class DummySkillHandler
    {
        public static string Execute(string userText)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            return $$"""
                {
                  "data_referencia": "{{today}}",
                  "divida_total_brl": 45000000.00,
                  "moedas": [
                    { "moeda": "USD", "principal": 5000000.00, "ptax": 5.30 },
                    { "moeda": "EUR", "principal": 3200000.00, "ptax": 5.91 },
                    { "moeda": "BRL", "principal": 18500000.00 }
                  ],
                  "consulta_original": {{JsonSerializer.Serialize(userText)}},
                  "fonte":  "SPIKE-POC",
                  "aviso":  "Dados fictícios para teste de conectividade A2A"
                }
                """;
        }
    }
}
