using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase4Lei4131Detail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lei4131_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sblc_numero = table.Column<string>(type: "text", nullable: true),
                    sblc_banco_emissor = table.Column<string>(type: "text", nullable: true),
                    sblc_valor_usd = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    tem_market_flex = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    break_funding_fee_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lei4131_detail", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lei4131_detail_contrato_id",
                schema: "sgcf",
                table: "lei4131_detail",
                column: "contrato_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lei4131_detail",
                schema: "sgcf");
        }
    }
}
