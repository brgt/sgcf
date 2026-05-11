using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finimp_detail",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rof_numero = table.Column<string>(type: "text", nullable: true),
                    rof_data_emissao = table.Column<LocalDate>(type: "date", nullable: true),
                    exportador_nome = table.Column<string>(type: "text", nullable: true),
                    exportador_pais = table.Column<string>(type: "text", nullable: true),
                    produto_importado = table.Column<string>(type: "text", nullable: true),
                    fatura_referencia = table.Column<string>(type: "text", nullable: true),
                    incoterm = table.Column<string>(type: "text", nullable: true),
                    break_funding_fee_percentual = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    tem_market_flex = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finimp_detail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametro_cotacao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banco_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modalidade = table.Column<string>(type: "text", nullable: true),
                    tipo_cotacao = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametro_cotacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plano_contas_gerencial",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_gerencial = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    natureza = table.Column<string>(type: "text", nullable: false),
                    codigo_sap_b1 = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plano_contas_gerencial", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                columns: new[] { "id", "ativo", "codigo_gerencial", "codigo_sap_b1", "created_at", "natureza", "nome", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), true, "1.1.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "ATIVO", "Conta Corrente em BRL", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0001-000000000002"), true, "1.1.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "ATIVO", "CDBs e Aplicações Livres", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0001-000000000003"), true, "1.2.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "ATIVO", "CDB Cativo (Cash Collateral)", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0001-000000000004"), true, "1.2.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "ATIVO", "Outras Garantias Bloqueadas", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0001-000000000005"), true, "1.3.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "ATIVO", "NDFs a Receber", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000001"), true, "2.1.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "FINIMP em Moeda Estrangeira", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000002"), true, "2.1.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "4131 em Moeda Estrangeira", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000003"), true, "2.1.3", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "NCE/CCE em BRL", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000004"), true, "2.1.4", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "Balcão Caixa", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000005"), true, "2.1.5", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "FGI (BNDES via Banco Intermediário)", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000006"), true, "2.1.6", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "REFINIMPs Ativos", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000007"), true, "2.2.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "NDFs a Pagar", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000008"), true, "2.3.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "Juros Provisionados FINIMP", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000009"), true, "2.3.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "Juros Provisionados 4131", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000010"), true, "2.3.3", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "Juros Provisionados Outros", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000011"), true, "2.4.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "IRRF s/ Juros Remetidos ao Exterior", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0002-000000000012"), true, "2.4.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "PASSIVO", "IOF Câmbio a Recolher", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000001"), true, "3.1.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Rendimento de CDB Cativo", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000002"), true, "3.1.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Ganho com NDF (MTM e Liquidação)", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000003"), true, "3.1.3", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Variação Cambial Ativa", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000004"), true, "3.2.1", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Juros sobre FINIMP", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000005"), true, "3.2.2", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Juros sobre 4131", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000006"), true, "3.2.3", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Juros sobre Demais Modalidades", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000007"), true, "3.2.4", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "IRRF Gross-Up", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000008"), true, "3.2.5", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "IOF Câmbio", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000009"), true, "3.2.6", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Comissões SBLC, CPG e Garantia", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000010"), true, "3.2.7", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Tarifas (ROF, CADEMP, Cartório)", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000011"), true, "3.2.8", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Perda com NDF (MTM e Liquidação)", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000012"), true, "3.2.9", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Variação Cambial Passiva", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) },
                    { new Guid("00000000-0000-0000-0003-000000000013"), true, "3.2.10", null, NodaTime.Instant.FromUnixTimeTicks(17784576000000000L), "RESULTADO", "Custo de Oportunidade do CDB Cativo", NodaTime.Instant.FromUnixTimeTicks(17784576000000000L) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_finimp_detail_contrato_id",
                schema: "sgcf",
                table: "finimp_detail",
                column: "contrato_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parametro_cotacao_banco_id_modalidade",
                schema: "sgcf",
                table: "parametro_cotacao",
                columns: new[] { "banco_id", "modalidade" });

            migrationBuilder.CreateIndex(
                name: "IX_plano_contas_gerencial_codigo_gerencial",
                schema: "sgcf",
                table: "plano_contas_gerencial",
                column: "codigo_gerencial",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finimp_detail",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "parametro_cotacao",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "plano_contas_gerencial",
                schema: "sgcf");
        }
    }
}
