using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S10_ParametroSistemaTetao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parametro_sistema",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    tetao_mensal_capacidade_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametro_sistema", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parametro_sistema_chave",
                schema: "sgcf",
                table: "parametro_sistema",
                column: "chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametro_sistema",
                schema: "sgcf");
        }
    }
}
