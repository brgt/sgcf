using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S7_CotacaoContratoMaeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "contrato_mae_id",
                schema: "sgcf",
                table: "cotacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cotacao_contrato_mae_id",
                schema: "sgcf",
                table: "cotacao",
                column: "contrato_mae_id",
                filter: "contrato_mae_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_cotacao_contrato_contrato_mae_id",
                schema: "sgcf",
                table: "cotacao",
                column: "contrato_mae_id",
                principalSchema: "sgcf",
                principalTable: "contrato",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cotacao_contrato_contrato_mae_id",
                schema: "sgcf",
                table: "cotacao");

            migrationBuilder.DropIndex(
                name: "ix_cotacao_contrato_mae_id",
                schema: "sgcf",
                table: "cotacao");

            migrationBuilder.DropColumn(
                name: "contrato_mae_id",
                schema: "sgcf",
                table: "cotacao");
        }
    }
}
