using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S24_PropostaTaxaIndicativaAa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "taxa_indicativa_aa_decimal",
                schema: "sgcf",
                table: "proposta",
                type: "numeric(10,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "taxa_indicativa_aa_decimal",
                schema: "sgcf",
                table: "proposta");
        }
    }
}
