using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S26_HistoricoMtmDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historico_mtm_diario",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hedge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_referencia = table.Column<LocalDate>(type: "date", nullable: false),
                    payoff_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    spot_utilizado = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    tipo_cotacao = table.Column<string>(type: "varchar(30)", nullable: false),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_mtm_diario", x => x.id);
                    table.ForeignKey(
                        name: "FK_historico_mtm_diario_instrumento_hedge_hedge_id",
                        column: x => x.hedge_id,
                        principalSchema: "sgcf",
                        principalTable: "instrumento_hedge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historico_mtm_diario_hedge_id",
                schema: "sgcf",
                table: "historico_mtm_diario",
                column: "hedge_id");

            migrationBuilder.CreateIndex(
                name: "ux_historico_mtm_diario_tenant_hedge_data",
                schema: "sgcf",
                table: "historico_mtm_diario",
                columns: new[] { "tenant_id", "hedge_id", "data_referencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historico_mtm_diario",
                schema: "sgcf");
        }
    }
}
