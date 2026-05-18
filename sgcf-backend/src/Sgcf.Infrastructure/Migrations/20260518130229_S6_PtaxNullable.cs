using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S6_PtaxNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ptax_usada_usd_brl",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric(12,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,6)");

            migrationBuilder.AlterColumn<LocalDate>(
                name: "data_ptax_referencia",
                schema: "sgcf",
                table: "cotacao",
                type: "date",
                nullable: true,
                oldClrType: typeof(LocalDate),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ptax_usada_usd_brl",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric(12,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<LocalDate>(
                name: "data_ptax_referencia",
                schema: "sgcf",
                table: "cotacao",
                type: "date",
                nullable: false,
                defaultValue: new NodaTime.LocalDate(1, 1, 1),
                oldClrType: typeof(LocalDate),
                oldType: "date",
                oldNullable: true);
        }
    }
}
