using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;
using Sgcf.Application.Simulacao.Cache;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;
using StackExchange.Redis;

namespace Sgcf.Infrastructure.Cache.Simulacao;

/// <summary>
/// Implementação Redis de <see cref="ICronogramaSimulacaoCache"/>.
///
/// <para>
/// <b>Chave de versão:</b> <c>sim:cronograma:{cenarioId:N}:{simulacaoId:N}:v{version}</c>
/// — o sufixo <c>:v{version}</c> implementa invalidação implícita: versões antigas
/// simplesmente nunca são consultadas após o incremento de <c>Version</c> no agregado.
/// </para>
///
/// <para>
/// <b>Índice de invalidação:</b> cada chave de versão é registrada em um Redis Set
/// <c>sim:cronograma:idx:{cenarioId:N}:{simulacaoId:N}</c>. Isso permite que
/// <see cref="InvalidarPorSimulacaoAsync"/> remova todas as versões de uma vez
/// sem precisar de varredura de padrão (<c>KEYS</c> / <c>SCAN</c>) — operação
/// proibida em produção.
/// </para>
///
/// <para>
/// <b>Serialização:</b> System.Text.Json com converters customizados para
/// <see cref="LocalDate"/> (ISO 8601: <c>yyyy-MM-dd</c>) e <see cref="Money"/>
/// (objeto JSON com campos <c>Valor</c> e <c>Moeda</c>).
/// </para>
/// </summary>
public sealed class RedisCronogramaSimulacaoCache(
    IConnectionMultiplexer redis,
    IOptions<CronogramaSimulacaoCacheOptions> options) : ICronogramaSimulacaoCache
{
    private const string KeyPrefix = "sim:cronograma";
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.TtlSeconds);

    // TTL do índice um pouco maior que o das entradas para garantir limpeza completa
    private TimeSpan IndiceExpiracao => _ttl + TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOpts = CriarJsonOptions();

    // ── Chaves ────────────────────────────────────────────────────────────────

    private static string ChavePorVersao(Guid cenarioId, Guid simulacaoId, int version) =>
        $"{KeyPrefix}:{cenarioId:N}:{simulacaoId:N}:v{version}";

    private static string IndicePorSimulacao(Guid cenarioId, Guid simulacaoId) =>
        $"{KeyPrefix}:idx:{cenarioId:N}:{simulacaoId:N}";

    // ── Interface pública ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EventoCronogramaGerado>?> GetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        CancellationToken cancellationToken = default)
    {
        IDatabase db = redis.GetDatabase();
        RedisValue val = await db.StringGetAsync(ChavePorVersao(cenarioId, simulacaoId, version));

        if (!val.HasValue)
        {
            return null;
        }

        string json = (string)val!;
        return JsonSerializer.Deserialize<List<EventoCronogramaGerado>>(json, JsonOpts);
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        IReadOnlyList<EventoCronogramaGerado> cronograma,
        CancellationToken cancellationToken = default)
    {
        IDatabase db = redis.GetDatabase();
        string chaveVersao = ChavePorVersao(cenarioId, simulacaoId, version);
        string chaveIndice  = IndicePorSimulacao(cenarioId, simulacaoId);
        string payload      = JsonSerializer.Serialize(cronograma, JsonOpts);

        // Pipeline atômico: persiste o cronograma e registra no índice
        ITransaction tx = db.CreateTransaction();
        _ = tx.StringSetAsync(chaveVersao, payload, _ttl);
        _ = tx.SetAddAsync(chaveIndice, chaveVersao);
        _ = tx.KeyExpireAsync(chaveIndice, IndiceExpiracao);
        await tx.ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task InvalidarPorSimulacaoAsync(
        Guid cenarioId,
        Guid simulacaoId,
        CancellationToken cancellationToken = default)
    {
        IDatabase db = redis.GetDatabase();
        string chaveIndice = IndicePorSimulacao(cenarioId, simulacaoId);

        // Lê todas as chaves registradas para esta simulação
        RedisValue[] membros = await db.SetMembersAsync(chaveIndice);

        if (membros.Length > 0)
        {
            // Remove as entradas de versão em batch
            RedisKey[] chaves = membros
                .Where(m => m.HasValue)
                .Select(m => (RedisKey)(string)m!)
                .ToArray();

            if (chaves.Length > 0)
            {
                await db.KeyDeleteAsync(chaves);
            }
        }

        // Remove o próprio índice
        await db.KeyDeleteAsync(chaveIndice);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EventoCronogramaGerado>> GetOrCreateAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        Func<Task<IReadOnlyList<EventoCronogramaGerado>>> factory,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventoCronogramaGerado>? cached =
            await GetAsync(cenarioId, simulacaoId, version, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        IReadOnlyList<EventoCronogramaGerado> calculado = await factory();
        await SetAsync(cenarioId, simulacaoId, version, calculado, cancellationToken);
        return calculado;
    }

    // ── Serialização ──────────────────────────────────────────────────────────

    private static JsonSerializerOptions CriarJsonOptions()
    {
        JsonSerializerOptions opts = new()
        {
            // Propriedades em PascalCase (padrão do domínio)
            PropertyNamingPolicy = null,
            WriteIndented = false,
        };

        opts.Converters.Add(new LocalDateJsonConverter());
        opts.Converters.Add(new MoneyJsonConverter());
        opts.Converters.Add(new JsonStringEnumConverter());

        return opts;
    }

    // ── Converters ────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializa <see cref="LocalDate"/> como string ISO 8601 (<c>yyyy-MM-dd</c>).
    /// Evita dependência de NodaTime.Serialization.SystemTextJson que não está no projeto.
    /// </summary>
    private sealed class LocalDateJsonConverter : JsonConverter<LocalDate>
    {
        private const string Format = "yyyy-MM-dd";

        public override LocalDate Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? valor = reader.GetString()
                ?? throw new JsonException("LocalDate não pode ser null.");

            if (!DateTime.TryParseExact(
                    valor, Format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime dt))
            {
                throw new JsonException($"Formato de data inválido: '{valor}'. Esperado: {Format}.");
            }

            return new LocalDate(dt.Year, dt.Month, dt.Day);
        }

        public override void Write(
            Utf8JsonWriter writer,
            LocalDate value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(
                string.Create(CultureInfo.InvariantCulture,
                    $"{value.Year:D4}-{value.Month:D2}-{value.Day:D2}"));
        }
    }

    /// <summary>
    /// Serializa <see cref="Money"/> como objeto JSON com campos <c>Valor</c> e <c>Moeda</c>.
    /// Necessário porque <see cref="Money"/> é um <c>readonly record struct</c> com construtor
    /// que aplica arredondamento — System.Text.Json não encontra o construtor correto sem este converter.
    /// </summary>
    private sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override Money Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Esperado StartObject para Money, encontrado {reader.TokenType}.");
            }

            decimal valor = 0m;
            Moeda moeda = Moeda.Brl;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Money(valor, moeda);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Esperado PropertyName no objeto Money.");
                }

                string propertyName = reader.GetString()!;
                reader.Read();

                if (propertyName == "Valor")
                {
                    valor = reader.GetDecimal();
                }
                else if (propertyName == "Moeda")
                {
                    string moedaNome = reader.GetString()
                        ?? throw new JsonException("Campo Moeda não pode ser null.");
                    moeda = Enum.Parse<Moeda>(moedaNome, ignoreCase: true);
                }
                else
                {
                    reader.Skip();
                }
            }

            throw new JsonException("JSON inesperadamente encerrado ao ler Money.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            Money value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Valor", value.Valor);
            writer.WriteString("Moeda", value.Moeda.ToString());
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Serializa <see cref="Moeda"/> como string (nome do enum) para legibilidade
    /// e estabilidade contra mudanças no valor numérico do enum.
    /// Usado apenas quando Moeda aparece fora de um objeto Money.
    /// </summary>
    private sealed class MoedaJsonConverter : JsonConverter<Moeda>
    {
        public override Moeda Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? nome = reader.GetString()
                ?? throw new JsonException("Moeda não pode ser null.");

            return Enum.Parse<Moeda>(nome, ignoreCase: true);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Moeda value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
