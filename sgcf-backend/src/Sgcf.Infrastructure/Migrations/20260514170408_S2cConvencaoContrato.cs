using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S2cConvencaoContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "convencao_data_nao_util",
                schema: "sgcf",
                table: "contrato",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1); // Following = 1
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "convencao_data_nao_util",
                schema: "sgcf",
                table: "contrato");
        }
    }
}
