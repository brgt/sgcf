using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sgcf.Application.Tenancy;

namespace Sgcf.Infrastructure.Persistence;

/// <summary>
/// EF Core connection interceptor que seta <c>app.tenant_id</c> via
/// <c>set_config</c> logo após a conexão ser aberta.
///
/// Essa configuração de sessão alimenta a política RLS <c>tenant_isolation</c>
/// definida em cada tabela tenant-scoped, tornando o RLS transparente para
/// toda query emitida pelo EF Core nessa conexão.
///
/// Quando <see cref="ITenantContext.IsResolved"/> é false (jobs de sistema,
/// migrations, seeds), o interceptor não faz nada e o RLS bloqueia todas
/// as linhas — comportamento seguro por padrão.
/// </summary>
internal sealed class TenantConnectionInterceptor(ITenantContext tenantContext)
    : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsResolved)
        {
            await SetTenantConfigAsync(connection, tenantContext.TenantId.ToString(), cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Versão síncrona necessária para cobrir caminhos de código que abrem
    /// a conexão sem await (e.g., algumas operações internas do EF Core).
    /// </remarks>
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        if (tenantContext.IsResolved)
        {
            // Caminho síncrono: ExecuteNonQuery direto.
            // Aceitável porque set_config é uma operação trivial de sessão Postgres.
            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.tenant_id', $1, false)";
            DbParameter p = cmd.CreateParameter();
            p.ParameterName = "$1";
            p.Value = tenantContext.TenantId.ToString();
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }

    private static async Task SetTenantConfigAsync(
        DbConnection connection,
        string tenantId,
        CancellationToken ct)
    {
        // false no 3º argumento de set_config = configuração de sessão (não transacional).
        // A configuração persiste durante toda a conexão, não apenas na transação atual.
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', $1, false)";
        DbParameter p = cmd.CreateParameter();
        p.ParameterName = "$1";
        p.Value = tenantId;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
