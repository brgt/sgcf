using Sgcf.Domain.Sistema;

namespace Sgcf.Application.Sistema;

/// <summary>
/// Repositório para <see cref="ParametroSistema"/> per-tenant.
///
/// O EF Core global query filter garante que <see cref="GetAsync"/> retorna
/// apenas o registro do tenant ativo no contexto atual — nenhum parâmetro
/// de tenant é passado explicitamente nos métodos de leitura.
/// </summary>
public interface IParametroSistemaRepository
{
    /// <summary>
    /// Retorna os parâmetros de sistema do tenant atual.
    /// Retorna <c>null</c> quando o tenant ainda não foi provisionado.
    /// </summary>
    public Task<ParametroSistema?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Verifica se já existe um registro de parâmetros de sistema para o tenant informado.
    /// Usado pelo provisionador para garantir idempotência no seed inicial.
    /// </summary>
    public Task<bool> ExisteParaTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Adiciona uma nova instância de parâmetros de sistema ao contexto (sem salvar).</summary>
    public void Add(ParametroSistema parametro);

    /// <summary>Persiste alterações pendentes no contexto.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
