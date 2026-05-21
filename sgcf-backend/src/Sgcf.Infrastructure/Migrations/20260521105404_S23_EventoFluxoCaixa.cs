using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S23_EventoFluxoCaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evento_fluxo_caixa",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<LocalDate>(type: "date", nullable: false),
                    tipo = table.Column<string>(type: "varchar(10)", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_moeda = table.Column<string>(type: "char(3)", nullable: false),
                    descricao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    registrado_por = table.Column<string>(type: "text", nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evento_fluxo_caixa", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evento_fluxo_caixa_tenant_data",
                schema: "sgcf",
                table: "evento_fluxo_caixa",
                columns: new[] { "tenant_id", "data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evento_fluxo_caixa",
                schema: "sgcf");
        }
    }
}
