using NodaTime;

namespace Sgcf.Application.Tenancy.Services;

/// <summary>
/// Provisiona os dados mestres iniciais de um tenant recém-criado.
/// A operação é idempotente: chamadas repetidas para o mesmo tenant
/// retornam <see cref="ResultadoProvisionamento"/> com contadores zerados
/// nos itens já existentes, sem criar duplicatas.
/// </summary>
public interface ITenantProvisioner
{
    /// <summary>
    /// Executa o seed dos dados mestres para o <paramref name="tenantId"/> informado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant a provisionar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    /// <returns>
    /// Resumo do provisionamento com contadores de registros criados e ignorados
    /// por categoria de dados mestres.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Quando o tenant não existe no sistema.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Quando o tenant está arquivado ou suspenso — estados inválidos para provisionamento.
    /// </exception>
    public Task<ResultadoProvisionamento> ProvisionarAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>
/// Resultado do provisionamento de um tenant, com contadores por categoria de dados mestres.
/// </summary>
/// <param name="TenantId">Id do tenant provisionado.</param>
/// <param name="TenantSlug">Slug do tenant provisionado.</param>
/// <param name="Criados">
/// Número de registros criados por categoria (chave = nome da categoria, valor = quantidade).
/// </param>
/// <param name="Ignorados">
/// Número de registros ignorados por já existirem (idempotência).
/// </param>
/// <param name="ProvisionadoEm">Instante em que o provisionamento foi executado.</param>
public sealed record ResultadoProvisionamento(
    Guid TenantId,
    string TenantSlug,
    IReadOnlyDictionary<string, int> Criados,
    IReadOnlyDictionary<string, int> Ignorados,
    Instant ProvisionadoEm);
