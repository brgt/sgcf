using System.Data;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Cria conexões brutas com o banco de dados, sem o overhead do EF Core e sem os interceptors
/// de tenant (TenantConnectionInterceptor). Usado exclusivamente pelo RlsHealthCheckService
/// para verificar o comportamento do RLS de fora do contexto de request normal.
/// </summary>
internal interface IDbConnectionFactory
{
    /// <summary>
    /// Cria e abre uma nova conexão ao banco de dados.
    /// O caller é responsável por descartar a conexão.
    /// </summary>
    public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct);
}
