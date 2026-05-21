using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S28_OrcamentoEncargo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "covenant",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "text", maxLength: 1000, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    periodicidade_verificacao_meses = table.Column<int>(type: "integer", nullable: false),
                    proxima_verificacao_em = table.Column<LocalDate>(type: "date", nullable: true),
                    ultima_verificacao_em = table.Column<LocalDate>(type: "date", nullable: true),
                    observacao_verificacao = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    limite_numerico = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    valor_apurado = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_covenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orcamento_encargo",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    tipo_encargo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    valor_orcado_brl_decimal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacao = table.Column<string>(type: "text", maxLength: 500, nullable: true),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orcamento_encargo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_covenant_contrato_id",
                schema: "sgcf",
                table: "covenant",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "ix_covenant_tenant_status",
                schema: "sgcf",
                table: "covenant",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_orcamento_encargo_periodo_tipo_banco_contrato",
                schema: "sgcf",
                table: "orcamento_encargo",
                columns: new[] { "tenant_id", "ano", "mes", "tipo_encargo", "banco_id", "contrato_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "covenant",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "orcamento_encargo",
                schema: "sgcf");
        }
    }
}
