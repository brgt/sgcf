using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S11_AddTenantIdToAllTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "snapshot_mensal_posicao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "simulacao_contratacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "simulacao_antecipacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "refinimp_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "proposta",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "posicao_snapshot",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "plano_contas_gerencial",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "parcela",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "parametro_sistema",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "parametro_cotacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "nce_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "limite_banco",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "lei4131_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "lancamento_contabil",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "instrumento_hedge",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_sblc_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_recebiveis_cartao_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_fgi_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_duplicatas_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_cdb_cativo_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_boleto_bancario_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_aval_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_alienacao_fiduciaria_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "finimp_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "fgi_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "economia_negociacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "ebitda_mensal",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "cronograma_pagamento",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "cotacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "contrato",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "cenario_simulacao",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "balcao_caixa_detail",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "audit_log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "alerta_vencimento",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "sgcf",
                table: "alerta_exposicao_banco",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tenant",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    cnpj_mascarado = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    plano = table.Column<byte>(type: "smallint", nullable: false),
                    criado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    suspenso_em = table.Column<Instant>(type: "timestamptz", nullable: true),
                    arquivado_em = table.Column<Instant>(type: "timestamptz", nullable: true),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.id);
                });

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000001"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000003"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000005"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000008"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000009"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000010"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000011"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000012"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000002"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000003"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000004"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000005"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000006"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000007"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000008"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000009"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            migrationBuilder.UpdateData(
                schema: "sgcf",
                table: "plano_contas_gerencial",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "tenant_id",
                value: new Guid("00000000-0000-7000-8000-000000000001"));

            // Seed do tenant Proxys — tenant padrão para desenvolvimento e dados legados.
            // Inserido antes dos UpdateData do plano_contas_gerencial para garantir
            // consistência referencial caso uma FK seja adicionada futuramente.
            migrationBuilder.Sql("""
                INSERT INTO sgcf.tenant (id, slug, nome, cnpj_mascarado, status, plano, criado_em, updated_at)
                VALUES (
                    '00000000-0000-7000-8000-000000000001',
                    'proxys',
                    'Proxys Group',
                    '**.***.***/****-**',
                    1,
                    1,
                    '2026-01-01T00:00:00Z',
                    '2026-01-01T00:00:00Z'
                )
                ON CONFLICT (id) DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_slug_unique",
                schema: "sgcf",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_status",
                schema: "sgcf",
                table: "tenant",
                column: "status",
                filter: "status <> 3");

            // Índices de performance: tenant_id nas tabelas com maior volume de leitura.
            // Particionamento lógico por tenant — queries filtradas por tenant_id beneficiam
            // destes índices, especialmente em ambientes multi-tenant com muitos tenants.
            migrationBuilder.CreateIndex(
                name: "ix_contrato_tenant",
                schema: "sgcf",
                table: "contrato",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_parcela_tenant",
                schema: "sgcf",
                table: "parcela",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotacao_tenant",
                schema: "sgcf",
                table: "cotacao",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant",
                schema: "sgcf",
                table: "audit_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cenario_simulacao_tenant",
                schema: "sgcf",
                table: "cenario_simulacao",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_cenario_simulacao_tenant", schema: "sgcf", table: "cenario_simulacao");
            migrationBuilder.DropIndex(name: "ix_audit_log_tenant", schema: "sgcf", table: "audit_log");
            migrationBuilder.DropIndex(name: "ix_cotacao_tenant", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropIndex(name: "ix_parcela_tenant", schema: "sgcf", table: "parcela");
            migrationBuilder.DropIndex(name: "ix_contrato_tenant", schema: "sgcf", table: "contrato");

            migrationBuilder.DropTable(
                name: "tenant",
                schema: "sgcf");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "snapshot_mensal_posicao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "simulacao_contratacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "simulacao_antecipacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "refinimp_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "proposta");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "posicao_snapshot");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "plano_contas_gerencial");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "parcela");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "parametro_sistema");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "parametro_cotacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "nce_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "limite_banco");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "lei4131_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "lancamento_contabil");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "instrumento_hedge");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_sblc_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_recebiveis_cartao_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_fgi_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_duplicatas_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_cdb_cativo_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_boleto_bancario_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_aval_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia_alienacao_fiduciaria_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "garantia");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "finimp_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "fgi_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "economia_negociacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "ebitda_mensal");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "cronograma_pagamento");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "cotacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "cenario_simulacao");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "balcao_caixa_detail");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "alerta_vencimento");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "sgcf",
                table: "alerta_exposicao_banco");
        }
    }
}
