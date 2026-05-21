using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <summary>
    /// Data migration: renomeia a chave legada "GLOBAL" para "DEFAULT" na tabela
    /// parametro_sistema.
    ///
    /// Context (Task −1.9): ParametroSistema migrou de singleton global para per-tenant.
    /// O discriminador Chave="GLOBAL" foi padronizado para "DEFAULT".
    /// Registros legados anteriores ao multi-tenant tinham chave "GLOBAL".
    /// </summary>
    public partial class S14_ParametroSistemaRenomearChaveGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renomeia registros legados "GLOBAL" → "DEFAULT" (idempotente).
            // O índice único (tenant_id, chave) garante que não haverá colisão
            // pois nunca existirá um tenant com tanto "GLOBAL" quanto "DEFAULT".
            migrationBuilder.Sql(@"
                UPDATE sgcf.parametro_sistema
                SET chave = 'DEFAULT'
                WHERE chave = 'GLOBAL';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverte "DEFAULT" → "GLOBAL" (idempotente).
            migrationBuilder.Sql(@"
                UPDATE sgcf.parametro_sistema
                SET chave = 'GLOBAL'
                WHERE chave = 'DEFAULT';
            ");
        }
    }
}
