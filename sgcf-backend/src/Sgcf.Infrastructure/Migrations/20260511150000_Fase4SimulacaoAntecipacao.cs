using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase4SimulacaoAntecipacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simulacao_antecipacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_antecipacao = table.Column<short>(type: "smallint", nullable: false),
                    data_simulacao = table.Column<Instant>(type: "timestamptz", nullable: false),
                    data_efetiva_proposta = table.Column<LocalDate>(type: "date", nullable: false),
                    valor_principal_a_quitar = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_principal_a_quitar_moeda = table.Column<short>(type: "smallint", nullable: false),
                    valor_total_simulado_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    cotacao_aplicada = table.Column<decimal>(type: "numeric(12,6)", nullable: true),
                    taxa_mercado_atual_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    padrao_aplicado = table.Column<short>(type: "smallint", nullable: false),
                    componentes_custo = table.Column<string>(type: "text", nullable: false),
                    economia_estimada_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    observacoes_banco = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "SIMULADA"),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulacao_antecipacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_simulacao_contrato",
                schema: "sgcf",
                table: "simulacao_antecipacao",
                columns: new[] { "contrato_id", "data_simulacao" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulacao_antecipacao",
                schema: "sgcf");
        }
    }
}
