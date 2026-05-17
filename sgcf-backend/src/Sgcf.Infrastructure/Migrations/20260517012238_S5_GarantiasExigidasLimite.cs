using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S5_GarantiasExigidasLimite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "limite_banco_garantia_exigida",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    limite_banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    percentual_sobre_limite = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    valor_fixo_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_banco_garantia_exigida", x => x.id);
                    table.CheckConstraint("ck_garantia_exigida_percentual_intervalo", "percentual_sobre_limite IS NULL OR (percentual_sobre_limite > 0 AND percentual_sobre_limite <= 100)");
                    table.CheckConstraint("ck_garantia_exigida_percentual_xor_valor", "(percentual_sobre_limite IS NULL AND valor_fixo_brl IS NULL AND tipo = 3) OR (percentual_sobre_limite IS NOT NULL AND valor_fixo_brl IS NULL) OR (percentual_sobre_limite IS NULL AND valor_fixo_brl IS NOT NULL)");
                    table.CheckConstraint("ck_garantia_exigida_valor_fixo_positivo", "valor_fixo_brl IS NULL OR valor_fixo_brl > 0");
                    table.ForeignKey(
                        name: "FK_limite_banco_garantia_exigida_limite_banco_limite_banco_id",
                        column: x => x.limite_banco_id,
                        principalSchema: "sgcf",
                        principalTable: "limite_banco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "limite_banco_historico",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    limite_banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_anterior_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    valor_novo_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_banco_historico", x => x.id);
                    table.ForeignKey(
                        name: "FK_limite_banco_historico_limite_banco_limite_banco_id",
                        column: x => x.limite_banco_id,
                        principalSchema: "sgcf",
                        principalTable: "limite_banco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_limite_banco",
                schema: "sgcf",
                table: "limite_banco_garantia_exigida",
                column: "limite_banco_id");

            migrationBuilder.CreateIndex(
                name: "ux_garantia_exigida_limite_tipo",
                schema: "sgcf",
                table: "limite_banco_garantia_exigida",
                columns: new[] { "limite_banco_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_limite_banco_historico_limite",
                schema: "sgcf",
                table: "limite_banco_historico",
                column: "limite_banco_id");

            migrationBuilder.CreateIndex(
                name: "ix_limite_banco_historico_limite_registrado_em",
                schema: "sgcf",
                table: "limite_banco_historico",
                columns: new[] { "limite_banco_id", "registrado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "limite_banco_garantia_exigida",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "limite_banco_historico",
                schema: "sgcf");
        }
    }
}
