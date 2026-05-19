using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S9_SimulacaoContratacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cenario_simulacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    ano_base = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    criado_por = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cenario_simulacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulacao_contratacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade = table.Column<string>(type: "text", nullable: false),
                    valor_principal = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    moeda = table.Column<short>(type: "smallint", nullable: false),
                    data_contratacao_prevista = table.Column<LocalDate>(type: "date", nullable: false),
                    data_primeiro_vencimento = table.Column<LocalDate>(type: "date", nullable: false),
                    tipo_taxa = table.Column<short>(type: "smallint", nullable: false),
                    taxa_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    spread_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    base_calculo = table.Column<short>(type: "smallint", nullable: false),
                    estrutura_amortizacao = table.Column<short>(type: "smallint", nullable: false),
                    periodicidade = table.Column<short>(type: "smallint", nullable: false),
                    quantidade_parcelas = table.Column<int>(type: "int", nullable: false),
                    anchor_dia_mes = table.Column<short>(type: "smallint", nullable: false),
                    anchor_dia_fixo = table.Column<int>(type: "int", nullable: true),
                    garantia_exigida_prevista = table.Column<string>(type: "text", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulacao_contratacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_simulacao_contratacao_cenario_simulacao_cenario_id",
                        column: x => x.cenario_id,
                        principalSchema: "sgcf",
                        principalTable: "cenario_simulacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cenario_simulacao_ano_base",
                schema: "sgcf",
                table: "cenario_simulacao",
                column: "ano_base",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cenario_simulacao_status_criado_por",
                schema: "sgcf",
                table: "cenario_simulacao",
                columns: new[] { "status", "criado_por" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_simulacao_contratacao_banco_id",
                schema: "sgcf",
                table: "simulacao_contratacao",
                column: "banco_id");

            migrationBuilder.CreateIndex(
                name: "IX_simulacao_contratacao_cenario_id",
                schema: "sgcf",
                table: "simulacao_contratacao",
                column: "cenario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulacao_contratacao",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "cenario_simulacao",
                schema: "sgcf");
        }
    }
}
