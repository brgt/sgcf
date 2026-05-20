using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S13_FixParametroSistemaUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parametro_sistema_chave",
                schema: "sgcf",
                table: "parametro_sistema");

            migrationBuilder.CreateIndex(
                name: "IX_parametro_sistema_tenant_id_chave",
                schema: "sgcf",
                table: "parametro_sistema",
                columns: new[] { "tenant_id", "chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parametro_sistema_tenant_id_chave",
                schema: "sgcf",
                table: "parametro_sistema");

            migrationBuilder.CreateIndex(
                name: "IX_parametro_sistema_chave",
                schema: "sgcf",
                table: "parametro_sistema",
                column: "chave",
                unique: true);
        }
    }
}
