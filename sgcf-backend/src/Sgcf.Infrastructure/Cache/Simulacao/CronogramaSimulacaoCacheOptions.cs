namespace Sgcf.Infrastructure.Cache.Simulacao;

/// <summary>
/// Opções de configuração para <see cref="RedisCronogramaSimulacaoCache"/>.
/// Mapeadas da seção <c>"CronogramaSimulacaoCache"</c> no appsettings.
/// </summary>
public sealed class CronogramaSimulacaoCacheOptions
{
    /// <summary>
    /// Tempo de vida (em segundos) de cada entrada no cache.
    /// Padrão: 60 s (AD-3 — TTL curto para dados de simulação hipotética).
    /// </summary>
    public int TtlSeconds { get; set; } = 60;
}
