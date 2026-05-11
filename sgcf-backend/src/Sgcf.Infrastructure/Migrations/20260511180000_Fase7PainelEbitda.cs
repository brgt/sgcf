using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase7PainelEbitda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ebitda_mensal",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    valor_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "varchar(200)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ebitda_mensal", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ebitda_mensal_ano_mes",
                schema: "sgcf",
                table: "ebitda_mensal",
                columns: new[] { "ano", "mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ebitda_mensal", schema: "sgcf");
        }
    }
}
