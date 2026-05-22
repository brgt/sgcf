using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S32_AntecipacaoParaBancoModalidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "break_funding_fee_pct",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.DropColumn(
                name: "observacoes_antecipacao",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.DropColumn(
                name: "padrao_antecipacao",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.DropColumn(
                name: "tla_pct_por_mes_remanescente",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.DropColumn(
                name: "tla_pct_sobre_saldo",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.DropColumn(
                name: "valor_minimo_parcial_pct",
                schema: "sgcf",
                table: "banco_config");

            migrationBuilder.AddColumn<decimal>(
                name: "break_funding_fee_pct",
                schema: "sgcf",
                table: "limite_banco",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes_antecipacao",
                schema: "sgcf",
                table: "limite_banco",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "padrao_antecipacao",
                schema: "sgcf",
                table: "limite_banco",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tla_pct_por_mes_remanescente",
                schema: "sgcf",
                table: "limite_banco",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tla_pct_sobre_saldo",
                schema: "sgcf",
                table: "limite_banco",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_minimo_parcial_pct",
                schema: "sgcf",
                table: "limite_banco",
                type: "numeric(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "break_funding_fee_pct",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "observacoes_antecipacao",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "padrao_antecipacao",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "tla_pct_por_mes_remanescente",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "tla_pct_sobre_saldo",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "valor_minimo_parcial_pct",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.AddColumn<decimal>(
                name: "break_funding_fee_pct",
                schema: "sgcf",
                table: "banco_config",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes_antecipacao",
                schema: "sgcf",
                table: "banco_config",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "padrao_antecipacao",
                schema: "sgcf",
                table: "banco_config",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "tla_pct_por_mes_remanescente",
                schema: "sgcf",
                table: "banco_config",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tla_pct_sobre_saldo",
                schema: "sgcf",
                table: "banco_config",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_minimo_parcial_pct",
                schema: "sgcf",
                table: "banco_config",
                type: "numeric(7,4)",
                nullable: true);
        }
    }
}
