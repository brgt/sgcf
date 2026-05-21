using System.Data;
using Npgsql;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Implementação de <see cref="IDbConnectionFactory"/> baseada em Npgsql.
/// Cria conexões diretas sem os EF Core interceptors — usado apenas pelo RLS healthcheck.
/// </summary>
internal sealed class NpgsqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
