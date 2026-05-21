using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S22_SaldoCaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saldo_caixa",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_referencia = table.Column<LocalDate>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_moeda = table.Column<string>(type: "char(3)", nullable: false),
                    registrado_por = table.Column<string>(type: "text", nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saldo_caixa", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saldo_caixa_conta_data",
                schema: "sgcf",
                table: "saldo_caixa",
                columns: new[] { "conta_id", "data_referencia" });

            migrationBuilder.CreateIndex(
                name: "ux_saldo_caixa_tenant_conta_data",
                schema: "sgcf",
                table: "saldo_caixa",
                columns: new[] { "tenant_id", "conta_id", "data_referencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saldo_caixa",
                schema: "sgcf");
        }
    }
}
