using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase4RefinimpDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refinimp_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_mae_id = table.Column<Guid>(type: "uuid", nullable: false),
                    percentual_refinanciado = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    valor_quitado_no_refi_valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_quitado_no_refi_moeda = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refinimp_detail", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refinimp_detail_contrato_id",
                schema: "sgcf",
                table: "refinimp_detail",
                column: "contrato_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refinimp_detail",
                schema: "sgcf");
        }
    }
}
