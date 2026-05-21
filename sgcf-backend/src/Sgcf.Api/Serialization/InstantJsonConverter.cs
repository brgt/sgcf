using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Text;

namespace Sgcf.Api.Serialization;

/// <summary>
/// Conversor <c>System.Text.Json</c> para <see cref="Instant"/>.
///
/// Serializa como string ISO 8601 UTC com sufixo <c>Z</c>
/// (ex: <c>"2026-05-21T12:00:00Z"</c>), compatível com a convenção do projeto.
///
/// Substitui a dependência opcional de <c>NodaTime.Serialization.SystemTextJson</c>
/// mantendo o mesmo formato de saída sem adicionar pacote externo.
/// </summary>
public sealed class InstantJsonConverter : JsonConverter<Instant>
{
    private static readonly InstantPattern Pattern = InstantPattern.General;

    /// <inheritdoc/>
    public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string text = reader.GetString()
            ?? throw new JsonException("Valor nulo não é permitido para Instant.");

        ParseResult<Instant> result = Pattern.Parse(text);
        if (!result.Success)
        {
            throw new JsonException(
                $"Não foi possível converter '{text}' para NodaTime.Instant. " +
                "Formato esperado: ISO 8601 UTC (ex: 2026-05-21T12:00:00Z).",
                result.Exception);
        }

        return result.Value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Pattern.Format(value));
}
