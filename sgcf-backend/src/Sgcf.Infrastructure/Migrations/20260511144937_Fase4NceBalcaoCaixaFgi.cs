using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase4NceBalcaoCaixaFgi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balcao_caixa_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_operacao = table.Column<string>(type: "text", nullable: true),
                    tipo_produto = table.Column<string>(type: "text", nullable: true),
                    tem_fgi = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balcao_caixa_detail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fgi_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_operacao_fgi = table.Column<string>(type: "text", nullable: true),
                    taxa_fgi_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    percentual_coberto = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fgi_detail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nce_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nce_numero = table.Column<string>(type: "text", nullable: true),
                    data_emissao = table.Column<LocalDate>(type: "date", nullable: true),
                    banco_mandatario = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nce_detail", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_balcao_caixa_detail_contrato_id",
                schema: "sgcf",
                table: "balcao_caixa_detail",
                column: "contrato_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fgi_detail_contrato_id",
                schema: "sgcf",
                table: "fgi_detail",
                column: "contrato_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nce_detail_contrato_id",
                schema: "sgcf",
                table: "nce_detail",
                column: "contrato_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balcao_caixa_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "fgi_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "nce_detail",
                schema: "sgcf");
        }
    }
}
