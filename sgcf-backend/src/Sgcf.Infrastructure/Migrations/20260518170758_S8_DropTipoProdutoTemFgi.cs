using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S8_DropTipoProdutoTemFgi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tem_fgi",
                schema: "sgcf",
                table: "balcao_caixa_detail");

            migrationBuilder.DropColumn(
                name: "tipo_produto",
                schema: "sgcf",
                table: "balcao_caixa_detail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tem_fgi",
                schema: "sgcf",
                table: "balcao_caixa_detail",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tipo_produto",
                schema: "sgcf",
                table: "balcao_caixa_detail",
                type: "text",
                nullable: true);
        }
    }
}
