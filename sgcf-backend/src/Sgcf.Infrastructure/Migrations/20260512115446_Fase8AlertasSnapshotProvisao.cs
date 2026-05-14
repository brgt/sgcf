using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase8AlertasSnapshotProvisao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_garantia_contrato_id_tipo_data_vigencia_ini",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "moeda",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.RenameColumn(
                name: "valor",
                schema: "sgcf",
                table: "garantia",
                newName: "valor_brl");

            migrationBuilder.RenameColumn(
                name: "data_vigencia_ini",
                schema: "sgcf",
                table: "garantia",
                newName: "data_constituicao");

            migrationBuilder.RenameColumn(
                name: "data_vigencia_fim",
                schema: "sgcf",
                table: "garantia",
                newName: "data_liberacao_prevista");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "sgcf",
                table: "simulacao_antecipacao",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "SIMULADA");

            migrationBuilder.AddColumn<Instant>(
                name: "created_at",
                schema: "sgcf",
                table: "garantia",
                type: "timestamptz",
                nullable: false,
                defaultValue: NodaTime.Instant.FromUnixTimeTicks(0L));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                schema: "sgcf",
                table: "garantia",
                type: "text",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_principal",
                schema: "sgcf",
                table: "garantia",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "updated_at",
                schema: "sgcf",
                table: "garantia",
                type: "timestamptz",
                nullable: false,
                defaultValue: NodaTime.Instant.FromUnixTimeTicks(0L));

            migrationBuilder.AddColumn<decimal>(
                name: "limite_credito_brl",
                schema: "sgcf",
                table: "banco_config",
                type: "numeric(20,6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alerta_exposicao_banco",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_alerta = table.Column<LocalDate>(type: "date", nullable: false),
                    exposicao_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    limite_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    percentual_ocupacao = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_exposicao_banco", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerta_vencimento",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_alerta = table.Column<string>(type: "varchar(20)", nullable: false),
                    data_vencimento = table.Column<LocalDate>(type: "date", nullable: false),
                    data_alerta = table.Column<LocalDate>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    moeda = table.Column<string>(type: "char(3)", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta_vencimento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebitda_mensal",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    valor_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "varchar(200)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ebitda_mensal", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "lancamento_contabil",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<LocalDate>(type: "date", nullable: false),
                    origem = table.Column<string>(type: "varchar(50)", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    moeda_contrato = table.Column<string>(type: "char(3)", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lancamento_contabil", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_mensal_posicao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    total_contratos_ativos = table.Column<int>(type: "integer", nullable: false),
                    saldo_principal_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    total_parcelas_abertas_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_mensal_posicao", x => x.id);
                });

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

            migrationBuilder.CreateIndex(
                name: "UX_alerta_exposicao_banco_banco_data",
                schema: "sgcf",
                table: "alerta_exposicao_banco",
                columns: new[] { "banco_id", "data_alerta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_alerta_vencimento_contrato_tipo_data",
                schema: "sgcf",
                table: "alerta_vencimento",
                columns: new[] { "contrato_id", "tipo_alerta", "data_vencimento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ebitda_mensal_ano_mes",
                schema: "sgcf",
                table: "ebitda_mensal",
                columns: new[] { "ano", "mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_alienacao_fiduciaria_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_alienacao_fiduciaria_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_aval_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_aval_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_boleto_bancario_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_boleto_bancario_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_cdb_cativo_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_cdb_cativo_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_duplicatas_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_duplicatas_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_fgi_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_fgi_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_recebiveis_cartao_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_recebiveis_cartao_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garantia_sblc_detail_garantia_id",
                schema: "sgcf",
                table: "garantia_sblc_detail",
                column: "garantia_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_lancamento_contabil_contrato_data_origem",
                schema: "sgcf",
                table: "lancamento_contabil",
                columns: new[] { "contrato_id", "data", "origem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_snapshot_mensal_posicao_ano_mes",
                schema: "sgcf",
                table: "snapshot_mensal_posicao",
                columns: new[] { "ano", "mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta_exposicao_banco",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "alerta_vencimento",
                schema: "sgcf");

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

            migrationBuilder.DropTable(
                name: "lancamento_contabil",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "snapshot_mensal_posicao",
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
                name: "limite_credito_brl",
                schema: "sgcf",
                table: "banco_config");

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
