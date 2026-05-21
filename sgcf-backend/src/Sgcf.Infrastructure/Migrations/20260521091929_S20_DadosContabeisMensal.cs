using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S20_DadosContabeisMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dados_contabeis_mensal",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    patrimonio_liquido_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    despesa_financeira_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    atualizado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dados_contabeis_mensal", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_dados_contabeis_tenant_competencia",
                schema: "sgcf",
                table: "dados_contabeis_mensal",
                columns: new[] { "tenant_id", "ano", "mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dados_contabeis_mensal",
                schema: "sgcf");
        }
    }
}
