using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S36_GarantiasAlternativas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "grupo_alternativa_id",
                schema: "sgcf",
                table: "garantia_exigida_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grupo_rotulo",
                schema: "sgcf",
                table: "garantia_exigida_item",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_item_revisao_grupo",
                schema: "sgcf",
                table: "garantia_exigida_item",
                columns: new[] { "revisao_id", "grupo_alternativa_id" },
                filter: "grupo_alternativa_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_garantia_exigida_item_revisao_grupo",
                schema: "sgcf",
                table: "garantia_exigida_item");

            migrationBuilder.DropColumn(
                name: "grupo_alternativa_id",
                schema: "sgcf",
                table: "garantia_exigida_item");

            migrationBuilder.DropColumn(
                name: "grupo_rotulo",
                schema: "sgcf",
                table: "garantia_exigida_item");
        }
    }
}
