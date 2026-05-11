using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase6HedgeMtm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "posicao_snapshot",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hedge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mtm_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    spot_utilizado = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    calculado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    tipo_cotacao = table.Column<string>(type: "varchar(50)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicao_snapshot", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posicao_snapshot_hedge_id_calculado_em",
                schema: "sgcf",
                table: "posicao_snapshot",
                columns: new[] { "hedge_id", "calculado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "posicao_snapshot", schema: "sgcf");
        }
    }
}
