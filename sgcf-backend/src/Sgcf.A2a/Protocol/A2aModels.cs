using System.Text.Json.Serialization;

namespace Sgcf.A2a.Protocol;

// ── Agent Card ────────────────────────────────────────────────────────────────

internal sealed record AgentCard(
    [property: JsonPropertyName("schemaVersion")]      string SchemaVersion,
    [property: JsonPropertyName("name")]               string Name,
    [property: JsonPropertyName("description")]        string Description,
    [property: JsonPropertyName("url")]                string Url,
    [property: JsonPropertyName("version")]            string Version,
    [property: JsonPropertyName("provider")]           AgentProvider Provider,
    [property: JsonPropertyName("capabilities")]       AgentCapabilities Capabilities,
    [property: JsonPropertyName("authentication")]     AgentAuth Authentication,
    [property: JsonPropertyName("defaultInputModes")]  string[] DefaultInputModes,
    [property: JsonPropertyName("defaultOutputModes")] string[] DefaultOutputModes,
    [property: JsonPropertyName("skills")]             AgentSkill[] Skills
);

internal sealed record AgentProvider(
    [property: JsonPropertyName("organization")] string Organization,
    [property: JsonPropertyName("url")]          string Url
);

internal sealed record AgentCapabilities(
    [property: JsonPropertyName("streaming")]              bool Streaming,
    [property: JsonPropertyName("pushNotifications")]      bool PushNotifications,
    [property: JsonPropertyName("stateTransitionHistory")] bool StateTransitionHistory
);

internal sealed record AgentAuth(
    [property: JsonPropertyName("schemes")] string[] Schemes
);

internal sealed record AgentSkill(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("tags")]        string[] Tags,
    [property: JsonPropertyName("examples")]    string[] Examples,
    [property: JsonPropertyName("inputModes")]  string[] InputModes,
    [property: JsonPropertyName("outputModes")] string[] OutputModes
);

// ── Task request / response ───────────────────────────────────────────────────

internal sealed record A2aTaskRequest(
    [property: JsonPropertyName("id")]      string? Id,
    [property: JsonPropertyName("message")] A2aMessage? Message
);

internal sealed record A2aTask(
    [property: JsonPropertyName("id")]     string Id,
    [property: JsonPropertyName("status")] A2aTaskStatus Status,
    [property: JsonPropertyName("result")] A2aMessage? Result
);

internal sealed record A2aTaskStatus(
    [property: JsonPropertyName("state")] string State   // "submitted" | "completed" | "failed"
);

internal sealed record A2aMessage(
    [property: JsonPropertyName("role")]  string Role,   // "user" | "agent"
    [property: JsonPropertyName("parts")] A2aPart[] Parts
);

internal sealed record A2aPart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text
);
