using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase5Garantias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Alter sgcf.garantia table ──────────────────────────────────

            // Rename valor → valor_brl
            migrationBuilder.RenameColumn(
                name: "valor",
                schema: "sgcf",
                table: "garantia",
                newName: "valor_brl");

            // Drop old moeda column (currency is always BRL in master table)
            migrationBuilder.DropColumn(
                name: "moeda",
                schema: "sgcf",
                table: "garantia");

            // Drop old unique index that referenced data_vigencia_ini (no longer a column)
            migrationBuilder.DropIndex(
                name: "IX_garantia_contrato_id_tipo_data_vigencia_ini",
                schema: "sgcf",
                table: "garantia");

            // Drop old date columns
            migrationBuilder.DropColumn(
                name: "data_vigencia_ini",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "data_vigencia_fim",
                schema: "sgcf",
                table: "garantia");

            // Add new columns — use server defaults for NOT NULL audit columns
            migrationBuilder.AddColumn<decimal>(
                name: "percentual_principal",
                schema: "sgcf",
                table: "garantia",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_constituicao",
                schema: "sgcf",
                table: "garantia",
                type: "date",
                nullable: false,
                defaultValue: new LocalDate(2026, 1, 1));

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_liberacao_prevista",
                schema: "sgcf",
                table: "garantia",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_liberacao_efetiva",
                schema: "sgcf",
                table: "garantia",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes",
                schema: "sgcf",
                table: "garantia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "sgcf",
                table: "garantia",
                type: "text",
                nullable: false,
                defaultValue: "migration");

            migrationBuilder.AddColumn<Instant>(
                name: "created_at",
                schema: "sgcf",
                table: "garantia",
                type: "timestamptz",
                nullable: false,
                defaultValue: NodaTime.Instant.FromUnixTimeTicks(17784576000000000L));

            migrationBuilder.AddColumn<Instant>(
                name: "updated_at",
                schema: "sgcf",
                table: "garantia",
                type: "timestamptz",
                nullable: false,
                defaultValue: NodaTime.Instant.FromUnixTimeTicks(17784576000000000L));

            // New indexes
            migrationBuilder.CreateIndex(
                name: "IX_garantia_contrato_id",
                schema: "sgcf",
                table: "garantia",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "IX_garantia_contrato_id_status",
                schema: "sgcf",
                table: "garantia",
                columns: new[] { "contrato_id", "status" });

            // ── 2. Create extension tables ─────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "garantia_cdb_cativo_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_custodia = table.Column<string>(type: "text", nullable: false),
                    numero_cdb = table.Column<string>(type: "text", nullable: false),
                    data_emissao_cdb = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vencimento_cdb = table.Column<LocalDate>(type: "date", nullable: false),
                    rendimento_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    percentual_cdi = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    taxa_irrf_aplicacao = table.Column<decimal>(type: "numeric(10,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_cdb_cativo_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_cdb_cativo_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_cdb_cativo_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_cdb_cativo_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_sblc_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_emissor = table.Column<string>(type: "text", nullable: false),
                    pais_emissor = table.Column<string>(type: "text", nullable: false),
                    swift_code = table.Column<string>(type: "text", nullable: true),
                    validade_dias = table.Column<int>(type: "integer", nullable: false),
                    comissao_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    numero_sblc = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_sblc_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_sblc_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_sblc_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_sblc_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_aval_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    avalista_tipo = table.Column<string>(type: "text", nullable: false),
                    avalista_nome = table.Column<string>(type: "text", nullable: false),
                    avalista_documento = table.Column<string>(type: "text", nullable: false),
                    valor_aval = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    vigencia_ate = table.Column<LocalDate>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_aval_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_aval_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_aval_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_aval_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_alienacao_fiduciaria_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_bem = table.Column<string>(type: "text", nullable: false),
                    descricao_bem = table.Column<string>(type: "text", nullable: false),
                    valor_avaliado = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    matricula_ou_chassi = table.Column<string>(type: "text", nullable: true),
                    cartorio_registro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_alienacao_fiduciaria_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_alienacao_fiduciaria_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_alienacao_fiduciaria_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_alienacao_fiduciaria_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_duplicatas_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    percentual_desconto = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    vencimento_escalonado_inicio = table.Column<LocalDate>(type: "date", nullable: false),
                    vencimento_escalonado_fim = table.Column<LocalDate>(type: "date", nullable: false),
                    qtd_duplicatas_cedidas = table.Column<int>(type: "integer", nullable: false),
                    valor_total_duplicatas = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    instrumento_cessao_data = table.Column<LocalDate>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_duplicatas_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_duplicatas_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_duplicatas_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_duplicatas_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_recebiveis_cartao_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operadora_cartao = table.Column<string>(type: "text", nullable: false),
                    tipo_recebivel = table.Column<string>(type: "text", nullable: false),
                    percentual_faturamento_comprometido = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    valor_medio_mensal_referencia = table.Column<decimal>(type: "numeric(20,6)", nullable: true),
                    prazo_recebimento_dias = table.Column<int>(type: "integer", nullable: false),
                    termo_cessao_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_recebiveis_cartao_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_recebiveis_cartao_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_recebiveis_cartao_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_recebiveis_cartao_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_boleto_bancario_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_emissor = table.Column<string>(type: "text", nullable: false),
                    quantidade_boletos = table.Column<int>(type: "integer", nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    data_emissao_inicial = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vencimento_inicial = table.Column<LocalDate>(type: "date", nullable: false),
                    data_vencimento_final = table.Column<LocalDate>(type: "date", nullable: false),
                    periodicidade = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_boleto_bancario_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_boleto_bancario_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_boleto_bancario_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_boleto_bancario_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateTable(
                name: "garantia_fgi_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garantia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_fgi = table.Column<string>(type: "text", nullable: false),
                    percentual_cobertura = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    taxa_fgi_aa = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    banco_intermediario = table.Column<string>(type: "text", nullable: true),
                    codigo_operacao_bndes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_fgi_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_fgi_detail_garantia_garantia_id",
                        column: x => x.garantia_id,
                        principalSchema: "sgcf",
                        principalTable: "garantia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garantia_fgi_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_fgi_detail",
                column: "garantia_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "garantia_cdb_cativo_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_sblc_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_aval_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_alienacao_fiduciaria_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_duplicatas_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_recebiveis_cartao_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_boleto_bancario_detail", schema: "sgcf");
            migrationBuilder.DropTable(name: "garantia_fgi_detail", schema: "sgcf");

            migrationBuilder.DropIndex(name: "IX_garantia_contrato_id", schema: "sgcf", table: "garantia");
            migrationBuilder.DropIndex(name: "IX_garantia_contrato_id_status", schema: "sgcf", table: "garantia");

            migrationBuilder.DropColumn(name: "percentual_principal", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "data_constituicao", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "data_liberacao_prevista", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "data_liberacao_efetiva", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "observacoes", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "created_by", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "created_at", schema: "sgcf", table: "garantia");
            migrationBuilder.DropColumn(name: "updated_at", schema: "sgcf", table: "garantia");

            migrationBuilder.RenameColumn(
                name: "valor_brl",
                schema: "sgcf",
                table: "garantia",
                newName: "valor");

            migrationBuilder.AddColumn<string>(
                name: "moeda",
                schema: "sgcf",
                table: "garantia",
                type: "char(3)",
                nullable: false,
                defaultValue: "BRL");

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_vigencia_ini",
                schema: "sgcf",
                table: "garantia",
                type: "date",
                nullable: false,
                defaultValue: new LocalDate(2026, 1, 1));

            migrationBuilder.AddColumn<LocalDate>(
                name: "data_vigencia_fim",
                schema: "sgcf",
                table: "garantia",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_contrato_id_tipo_data_vigencia_ini",
                schema: "sgcf",
                table: "garantia",
                columns: new[] { "contrato_id", "tipo", "data_vigencia_ini" },
                unique: true);
        }
    }
}
