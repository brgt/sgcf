using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S37_RegimeLimiteBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "regime_limite",
                schema: "sgcf",
                table: "banco_config",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill — preserva a semântica implícita anterior (SPEC_LIMITE_GLOBAL §4.3):
            // bancos que hoje operam de fato como "global puro" (têm limite global em aberto
            // e nenhum LimiteBanco ativo, em qualquer tenant) recebem GlobalPuro (1).
            // Os demais permanecem PerModalidade (0, default da coluna).
            migrationBuilder.Sql(@"
                UPDATE sgcf.banco_config b
                SET regime_limite = 1
                WHERE EXISTS (
                        SELECT 1 FROM sgcf.limite_global_banco g
                        WHERE g.banco_id = b.id
                          AND g.data_vigencia_fim IS NULL
                      )
                  AND NOT EXISTS (
                        SELECT 1 FROM sgcf.limite_banco lb
                        WHERE lb.banco_id = b.id
                          AND lb.data_vigencia_fim IS NULL
                      );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "regime_limite",
                schema: "sgcf",
                table: "banco_config");
        }
    }
}
