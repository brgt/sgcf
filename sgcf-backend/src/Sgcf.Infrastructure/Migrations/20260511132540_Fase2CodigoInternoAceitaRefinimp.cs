using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase2CodigoInternoAceitaRefinimp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_interno",
                schema: "sgcf",
                table: "contrato",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "aceita_refinimp",
                schema: "sgcf",
                table: "banco_config",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrato_codigo_interno",
                schema: "sgcf",
                table: "contrato",
                column: "codigo_interno",
                unique: true,
                filter: "codigo_interno IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contrato_codigo_interno",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "codigo_interno",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "aceita_refinimp",
                schema: "sgcf",
                table: "banco_config");
        }
    }
}
