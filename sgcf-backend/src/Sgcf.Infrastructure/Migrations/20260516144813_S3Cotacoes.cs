using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S3Cotacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cdi_snapshot",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<LocalDate>(type: "date", nullable: false),
                    cdi_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cdi_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cotacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_interno = table.Column<string>(type: "text", nullable: false),
                    modalidade = table.Column<string>(type: "text", nullable: false),
                    valor_alvo_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    prazo_maximo_dias = table.Column<int>(type: "integer", nullable: false),
                    data_abertura = table.Column<LocalDate>(type: "date", nullable: false),
                    data_ptax_referencia = table.Column<LocalDate>(type: "date", nullable: false),
                    ptax_usada_usd_brl = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    proposta_aceita_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contrato_gerado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aceita_por = table.Column<string>(type: "text", nullable: true),
                    data_aceitacao = table.Column<Instant>(type: "timestamptz", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamptz", nullable: true),
                    bancos_alvo_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_cotacao_contrato_contrato_gerado_id",
                        column: x => x.contrato_gerado_id,
                        principalSchema: "sgcf",
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "limite_banco",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade = table.Column<string>(type: "text", nullable: false),
                    valor_limite_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    valor_utilizado_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    data_vigencia_inicio = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vigencia_fim = table.Column<LocalDate>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_banco", x => x.id);
                    table.ForeignKey(
                        name: "FK_limite_banco_banco_config_banco_id",
                        column: x => x.banco_id,
                        principalSchema: "sgcf",
                        principalTable: "banco_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "economia_negociacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cotacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_proposta_json = table.Column<string>(type: "jsonb", nullable: false),
                    snapshot_contrato_json = table.Column<string>(type: "jsonb", nullable: false),
                    cet_proposta_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    cet_contrato_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    economia_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    economia_ajustada_cdi_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    data_referencia_cdi = table.Column<LocalDate>(type: "date", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economia_negociacao", x => x.id);
                    table.ForeignKey(
                        name: "FK_economia_negociacao_contrato_contrato_id",
                        column: x => x.contrato_id,
                        principalSchema: "sgcf",
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_economia_negociacao_cotacao_cotacao_id",
                        column: x => x.cotacao_id,
                        principalSchema: "sgcf",
                        principalTable: "cotacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposta",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cotacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moeda_original = table.Column<string>(type: "char(3)", nullable: false),
                    valor_oferecido_moeda_original = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    taxa_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    iof_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    spread_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    prazo_dias = table.Column<int>(type: "integer", nullable: false),
                    estrutura_amortizacao = table.Column<short>(type: "smallint", nullable: false),
                    periodicidade_juros = table.Column<short>(type: "smallint", nullable: false),
                    exige_ndf = table.Column<bool>(type: "boolean", nullable: false),
                    custo_ndf_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    garantia_exigida = table.Column<string>(type: "text", nullable: false),
                    valor_garantia_exigida_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    garantia_eh_cdb_cativo = table.Column<bool>(type: "boolean", nullable: false),
                    rendimento_cdb_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    cet_calculado_aa_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    valor_total_estimado_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    data_captura = table.Column<LocalDate>(type: "date", nullable: false),
                    data_validade_mercado = table.Column<LocalDate>(type: "date", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    motivo_recusa = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposta", x => x.id);
                    table.ForeignKey(
                        name: "FK_proposta_banco_config_banco_id",
                        column: x => x.banco_id,
                        principalSchema: "sgcf",
                        principalTable: "banco_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposta_cotacao_cotacao_id",
                        column: x => x.cotacao_id,
                        principalSchema: "sgcf",
                        principalTable: "cotacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cdi_snapshot_data",
                schema: "sgcf",
                table: "cdi_snapshot",
                column: "data",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_codigo_interno",
                schema: "sgcf",
                table: "cotacao",
                column: "codigo_interno",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_contrato_gerado_id",
                schema: "sgcf",
                table: "cotacao",
                column: "contrato_gerado_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_data_abertura",
                schema: "sgcf",
                table: "cotacao",
                column: "data_abertura",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_modalidade",
                schema: "sgcf",
                table: "cotacao",
                column: "modalidade",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cotacao_status",
                schema: "sgcf",
                table: "cotacao",
                column: "status",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_economia_negociacao_contrato_id",
                schema: "sgcf",
                table: "economia_negociacao",
                column: "contrato_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_economia_negociacao_cotacao_id",
                schema: "sgcf",
                table: "economia_negociacao",
                column: "cotacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_limite_banco_banco_id_modalidade",
                schema: "sgcf",
                table: "limite_banco",
                columns: new[] { "banco_id", "modalidade" },
                unique: true,
                filter: "data_vigencia_fim IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_proposta_banco_id",
                schema: "sgcf",
                table: "proposta",
                column: "banco_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposta_cotacao_id",
                schema: "sgcf",
                table: "proposta",
                column: "cotacao_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposta_status",
                schema: "sgcf",
                table: "proposta",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cdi_snapshot",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "economia_negociacao",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "limite_banco",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "proposta",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "cotacao",
                schema: "sgcf");
        }
    }
}
