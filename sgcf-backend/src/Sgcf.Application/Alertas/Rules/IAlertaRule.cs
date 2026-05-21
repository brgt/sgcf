using NodaTime;

namespace Sgcf.Application.Alertas.Rules;

/// <summary>
/// Contrato para regras do rules engine de alertas.
/// Cada implementação encapsula a lógica de avaliação de uma categoria específica de alerta
/// e persiste os alertas gerados via <see cref="IAlertaRepository.TryAddIdempotentAsync"/>.
/// </summary>
public interface IAlertaRule
{
    /// <summary>
    /// Identificador único da regra. Usado como prefixo na <c>ChaveIdempotencia</c>
    /// para garantir unicidade entre regras distintas.
    /// </summary>
    public string Nome { get; }

    /// <summary>
    /// Avalia a regra para o tenant corrente (resolvido via <see cref="ITenantContext"/>)
    /// e persiste os alertas gerados de forma idempotente.
    /// O contexto de tenant deve estar resolvido no escopo atual antes de chamar este método.
    /// </summary>
    /// <param name="hoje">Data de referência em horário de Brasília — nunca <c>DateTime.Now</c>.</param>
    /// <param name="ct">Token de cancelamento propagado do hosted service.</param>
    public Task AvaliarAsync(LocalDate hoje, CancellationToken ct);
}
