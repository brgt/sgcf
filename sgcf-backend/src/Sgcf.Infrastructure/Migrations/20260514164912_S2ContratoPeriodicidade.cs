using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S2ContratoPeriodicidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: garantia table refactoring (DropIndex, DropColumn moeda, RenameColumns, AddColumns)
            // and ebitda_mensal + garantia_*_detail table creation were already applied via Fase5Garantias
            // and Fase7PainelEbitda migrations. Only the contrato columns are added here.

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "sgcf",
                table: "simulacao_antecipacao",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "SIMULADA");

            migrationBuilder.AddColumn<int>(
                name: "anchor_dia_fixo",
                schema: "sgcf",
                table: "contrato",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "anchor_dia_mes",
                schema: "sgcf",
                table: "contrato",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_primeiro_vencimento",
                schema: "sgcf",
                table: "contrato",
                type: "date",
                nullable: false,
                defaultValue: new NodaTime.LocalDate(1, 1, 1));

            migrationBuilder.AddColumn<short>(
                name: "estrutura_amortizacao",
                schema: "sgcf",
                table: "contrato",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "periodicidade",
                schema: "sgcf",
                table: "contrato",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "periodicidade_juros",
                schema: "sgcf",
                table: "contrato",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantidade_parcelas",
                schema: "sgcf",
                table: "contrato",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: existing rows use sensible defaults for Bullet/single-payment contracts.
            // Periodicidade.Bullet=1, EstruturaAmortizacao.Bullet=1, AnchorDiaMes.DiaContratacao=1, QuantidadeParcelas=1.
            migrationBuilder.Sql(
                "UPDATE sgcf.contrato SET data_primeiro_vencimento = data_vencimento WHERE data_primeiro_vencimento = '0001-01-01';");
            migrationBuilder.Sql(
                "UPDATE sgcf.contrato SET periodicidade = 1 WHERE periodicidade = 0;");
            migrationBuilder.Sql(
                "UPDATE sgcf.contrato SET estrutura_amortizacao = 1 WHERE estrutura_amortizacao = 0;");
            migrationBuilder.Sql(
                "UPDATE sgcf.contrato SET anchor_dia_mes = 1 WHERE anchor_dia_mes = 0;");
            migrationBuilder.Sql(
                "UPDATE sgcf.contrato SET quantidade_parcelas = 1 WHERE quantidade_parcelas = 0;");

            // Tables ebitda_mensal, garantia_*_detail and their indexes were already created
            // by Fase5Garantias and Fase7PainelEbitda — no CreateTable/CreateIndex needed here.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ebitda_mensal",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_alienacao_fiduciaria_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_aval_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_boleto_bancario_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_cdb_cativo_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_duplicatas_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_fgi_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_recebiveis_cartao_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_sblc_detail",
                schema: "sgcf");

            migrationBuilder.DropIndex(
                name: "IX_garantia_contrato_id",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropIndex(
                name: "IX_garantia_contrato_id_status",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "data_liberacao_efetiva",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "observacoes",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "percentual_principal",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "anchor_dia_fixo",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "anchor_dia_mes",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "data_primeiro_vencimento",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "estrutura_amortizacao",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "periodicidade",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "periodicidade_juros",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "quantidade_parcelas",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.RenameColumn(
                name: "valor_brl",
                schema: "sgcf",
                table: "garantia",
                newName: "valor");

            migrationBuilder.RenameColumn(
                name: "data_liberacao_prevista",
                schema: "sgcf",
                table: "garantia",
                newName: "data_vigencia_fim");

            migrationBuilder.RenameColumn(
                name: "data_constituicao",
                schema: "sgcf",
                table: "garantia",
                newName: "data_vigencia_ini");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "sgcf",
                table: "simulacao_antecipacao",
                type: "text",
                nullable: false,
                defaultValue: "SIMULADA",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "moeda",
                schema: "sgcf",
                table: "garantia",
                type: "char(3)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_garantia_contrato_id_tipo_data_vigencia_ini",
                schema: "sgcf",
                table: "garantia",
                columns: new[] { "contrato_id", "tipo", "data_vigencia_ini" },
                unique: true);
        }
    }
}
