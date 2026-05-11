using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable
#pragma warning disable CA1861

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sgcf");

            migrationBuilder.CreateTable(
                name: "banco_config",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_compe = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false),
                    razao_social = table.Column<string>(type: "text", nullable: false),
                    apelido = table.Column<string>(type: "text", nullable: false),
                    aceita_liquidacao_total = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    aceita_liquidacao_parcial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    exige_anuencia_expressa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    exige_parcela_inteira = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    aviso_previo_min_dias_uteis = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    valor_minimo_parcial_pct = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    padrao_antecipacao = table.Column<short>(type: "smallint", nullable: false),
                    break_funding_fee_pct = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    tla_pct_sobre_saldo = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    tla_pct_por_mes_remanescente = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    observacoes_antecipacao = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banco_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contrato",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_externo = table.Column<string>(type: "text", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade = table.Column<string>(type: "text", nullable: false),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    valor_principal = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    data_contratacao = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: false),
                    taxa_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    base_calculo = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    contrato_pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cotacao_fx",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    moeda_base = table.Column<string>(type: "char(3)", nullable: false),
                    moeda_quote = table.Column<string>(type: "char(3)", nullable: false),
                    momento = table.Column<Instant>(type: "timestamptz", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    valor_compra = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_venda = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    fonte = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotacao_fx", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instrumento_hedge",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    contraparte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notional = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    moeda_base = table.Column<string>(type: "char(3)", nullable: false),
                    moeda_quote = table.Column<string>(type: "char(3)", nullable: false),
                    data_contratacao = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: false),
                    strike_forward = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    strike_put = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    strike_call = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrumento_hedge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "garantia",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    data_vigencia_ini = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vigencia_fim = table.Column<LocalDate>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_contrato_contrato_id",
                        column: x => x.contrato_id,
                        principalSchema: "sgcf",
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parcela",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<short>(type: "smallint", nullable: false),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: false),
                    valor_principal = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_juros = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_pago = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    data_pagamento = table.Column<LocalDate>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcela", x => x.id);
                    table.ForeignKey(
                        name: "FK_parcela_contrato_contrato_id",
                        column: x => x.contrato_id,
                        principalSchema: "sgcf",
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_banco_config_codigo_compe",
                schema: "sgcf",
                table: "banco_config",
                column: "codigo_compe",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrato_banco_id",
                schema: "sgcf",
                table: "contrato",
                column: "banco_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_contrato_pai_id",
                schema: "sgcf",
                table: "contrato",
                column: "contrato_pai_id",
                filter: "contrato_pai_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_data_vencimento",
                schema: "sgcf",
                table: "contrato",
                column: "data_vencimento",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_status",
                schema: "sgcf",
                table: "contrato",
                column: "status",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_fx_moeda_base_moeda_quote_momento_tipo",
                schema: "sgcf",
                table: "cotacao_fx",
                columns: new[] { "moeda_base", "moeda_quote", "momento", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_fx_moeda_base_tipo_momento",
                schema: "sgcf",
                table: "cotacao_fx",
                columns: new[] { "moeda_base", "tipo", "momento" });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_contrato_id_tipo_data_vigencia_ini",
                schema: "sgcf",
                table: "garantia",
                columns: new[] { "contrato_id", "tipo", "data_vigencia_ini" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instrumento_hedge_contrato_id",
                schema: "sgcf",
                table: "instrumento_hedge",
                column: "contrato_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcela_contrato_id_numero",
                schema: "sgcf",
                table: "parcela",
                columns: new[] { "contrato_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parcela_data_vencimento",
                schema: "sgcf",
                table: "parcela",
                column: "data_vencimento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banco_config",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "cotacao_fx",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "instrumento_hedge",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "parcela",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "contrato",
                schema: "sgcf");
        }
    }
}
