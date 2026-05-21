using NodaTime;

namespace Sgcf.Application.Common;

/// <summary>
/// Envelope padronizado para respostas de endpoints que expõem dados calculados ou agregados.
///
/// O envelope separa claramente o payload de negócio (<see cref="Data"/>) dos metadados
/// de observabilidade (<see cref="Meta"/>), permitindo que o cliente saiba quão frescos
/// e completos são os dados que recebeu.
///
/// Serializado em JSON como:
/// <code>
/// {
///   "data": { ... },
///   "meta": {
///     "dataHoraCalculo": "...",
///     "fontesConsultadas": [...],
///     "completude": "Completo"
///   }
/// }
/// </code>
/// </summary>
public sealed record EnvelopeResponse<T>(T Data, EnvelopeMeta Meta);

/// <summary>
/// Metadados de observabilidade incluídos em toda resposta envelopada.
///
/// <para>
/// <see cref="DataHoraCalculo"/>: instante UTC em que a resposta foi montada,
/// capturado via <c>IClock</c> (NodaTime) — nunca <c>DateTime.UtcNow</c>.
/// </para>
/// <para>
/// <see cref="FontesConsultadas"/>: lista das fontes de dados efetivamente consultadas
/// para montar a resposta (ex: banco de dados, cache, API externa).
/// Handlers podem enriquecer essa lista retornando um <see cref="EnvelopeResponse{T}"/>
/// diretamente; o filtro usa a lista vazia como valor padrão mínimo.
/// </para>
/// <para>
/// <see cref="Completude"/>: indica se todos os dados esperados estavam disponíveis.
/// Use <see cref="Completude.Parcial"/> quando alguma fonte retornou dados incompletos
/// e <see cref="Completude.Degradado"/> quando uma fonte principal falhou mas a
/// resposta ainda foi retornada com dados alternativos (ex: fallback de cache).
/// </para>
/// </summary>
public sealed record EnvelopeMeta(
    Instant DataHoraCalculo,
    IReadOnlyList<FonteConsultada> FontesConsultadas,
    Completude Completude);

/// <summary>
/// Descreve uma única fonte de dados consultada durante o processamento da requisição.
/// </summary>
/// <param name="Fonte">
/// Nome identificador da fonte (ex: <c>"banco_de_dados"</c>, <c>"cache_redis"</c>,
/// <c>"api_bcb"</c>).
/// </param>
/// <param name="Status">
/// Estado da consulta (ex: <c>"ok"</c>, <c>"timeout"</c>, <c>"cache_hit"</c>,
/// <c>"indisponivel"</c>).
/// </param>
/// <param name="Registros">
/// Número de registros retornados pela fonte, quando aplicável.
/// <c>null</c> para fontes que não retornam contagem (ex: cache de configuração).
/// </param>
public sealed record FonteConsultada(string Fonte, string Status, int? Registros);

/// <summary>
/// Indica o grau de completude dos dados retornados no envelope.
/// </summary>
public enum Completude
{
    /// <summary>Todas as fontes responderam com sucesso e os dados estão completos.</summary>
    Completo,

    /// <summary>
    /// Pelo menos uma fonte retornou dados incompletos (ex: período parcialmente coberto),
    /// mas a resposta é utilizável.
    /// </summary>
    Parcial,

    /// <summary>
    /// Uma fonte principal falhou e a resposta foi construída com dados alternativos
    /// (ex: cache stale, fallback). A resposta pode estar desatualizada.
    /// </summary>
    Degradado
}
