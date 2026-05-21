using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sgcf.Infrastructure.Migrations
{
    /// <summary>
    /// Migration S15: PlanoContasModelo (global) + per-tenant PlanoContasGerencial.
    ///
    /// Operações:
    /// 1. Drop índice único antigo em codigo_gerencial (era global, sem tenant_id).
    /// 2. Adiciona coluna clonada_de_modelo.
    /// 3. Cria tabela plano_contas_modelo (global).
    /// 4. Cria índice único composto (tenant_id, codigo_gerencial) em plano_contas_gerencial.
    /// 5. Cria índice único em plano_contas_modelo.codigo_gerencial.
    /// 6. Seed do modelo padrão via SQL — mesmo conjunto do HasData removido do EF config.
    /// 7. Marca rows existentes do tenant Proxys como clonada_de_modelo = TRUE.
    ///
    /// Decisão: os registros do tenant Proxys inseridos pela migration S11 (HasData)
    /// PERMANECEM na tabela — apenas deixamos de gerenciá-los via HasData. Os DeleteData
    /// gerados automaticamente pelo EF foram removidos desta migration para não destruir
    /// dados existentes do tenant.
    /// </summary>
    public partial class S15_PlanoContasModeloEPerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop índice antigo (era global, sem tenant_id — violaria isolação multi-tenant).
            migrationBuilder.DropIndex(
                name: "IX_plano_contas_gerencial_codigo_gerencial",
                schema: "sgcf",
                table: "plano_contas_gerencial");

            // 2. Adiciona coluna clonada_de_modelo com default FALSE.
            migrationBuilder.AddColumn<bool>(
                name: "clonada_de_modelo",
                schema: "sgcf",
                table: "plano_contas_gerencial",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 3. Cria tabela do modelo global.
            migrationBuilder.CreateTable(
                name: "plano_contas_modelo",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_gerencial = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    natureza = table.Column<string>(type: "text", nullable: false),
                    codigo_sap_b1 = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plano_contas_modelo", x => x.id);
                });

            // 4. Índice único composto (tenant_id, codigo_gerencial) — garante isolação per-tenant.
            migrationBuilder.CreateIndex(
                name: "ix_plano_contas_gerencial_tenant_codigo",
                schema: "sgcf",
                table: "plano_contas_gerencial",
                columns: new[] { "tenant_id", "codigo_gerencial" },
                unique: true);

            // 5. Índice único no modelo global.
            migrationBuilder.CreateIndex(
                name: "IX_plano_contas_modelo_codigo_gerencial",
                schema: "sgcf",
                table: "plano_contas_modelo",
                column: "codigo_gerencial",
                unique: true);

            // 6. Seed do modelo padrão — mesmo conjunto de contas do HasData anterior.
            //    Usa gen_random_uuid() para IDs: o modelo não precisa de IDs estáveis
            //    (nunca é referenciado por FK, apenas lido para clonar).
            migrationBuilder.Sql(@"
                INSERT INTO sgcf.plano_contas_modelo
                    (id, codigo_gerencial, nome, natureza, codigo_sap_b1, created_at, updated_at)
                VALUES
                    (gen_random_uuid(), '1.1.1', 'Conta Corrente em BRL',                    'ATIVO',     NULL, NOW(), NOW()),
                    (gen_random_uuid(), '1.1.2', 'CDBs e Aplicações Livres',                 'ATIVO',     NULL, NOW(), NOW()),
                    (gen_random_uuid(), '1.2.1', 'CDB Cativo (Cash Collateral)',              'ATIVO',     NULL, NOW(), NOW()),
                    (gen_random_uuid(), '1.2.2', 'Outras Garantias Bloqueadas',              'ATIVO',     NULL, NOW(), NOW()),
                    (gen_random_uuid(), '1.3.1', 'NDFs a Receber',                           'ATIVO',     NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.1', 'FINIMP em Moeda Estrangeira',              'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.2', '4131 em Moeda Estrangeira',                'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.3', 'NCE/CCE em BRL',                           'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.4', 'Balcão Caixa',                             'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.5', 'FGI (BNDES via Banco Intermediário)',      'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.1.6', 'REFINIMPs Ativos',                         'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.2.1', 'NDFs a Pagar',                             'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.3.1', 'Juros Provisionados FINIMP',               'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.3.2', 'Juros Provisionados 4131',                 'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.3.3', 'Juros Provisionados Outros',               'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.4.1', 'IRRF s/ Juros Remetidos ao Exterior',     'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '2.4.2', 'IOF Câmbio a Recolher',                    'PASSIVO',   NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.1.1', 'Rendimento de CDB Cativo',                 'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.1.2', 'Ganho com NDF (MTM e Liquidação)',         'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.1.3', 'Variação Cambial Ativa',                   'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.1', 'Juros sobre FINIMP',                       'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.2', 'Juros sobre 4131',                         'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.3', 'Juros sobre Demais Modalidades',           'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.4', 'IRRF Gross-Up',                            'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.5', 'IOF Câmbio',                               'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.6', 'Comissões SBLC, CPG e Garantia',           'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.7', 'Tarifas (ROF, CADEMP, Cartório)',          'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.8', 'Perda com NDF (MTM e Liquidação)',         'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.9', 'Variação Cambial Passiva',                 'RESULTADO', NULL, NOW(), NOW()),
                    (gen_random_uuid(), '3.2.10', 'Custo de Oportunidade do CDB Cativo',    'RESULTADO', NULL, NOW(), NOW())
                ON CONFLICT (codigo_gerencial) DO NOTHING;
            ");

            // 7. Marca registros do tenant Proxys como clonada_de_modelo = TRUE
            //    quando o código bate com o modelo (idempotente).
            migrationBuilder.Sql(@"
                UPDATE sgcf.plano_contas_gerencial pcg
                SET clonada_de_modelo = TRUE
                WHERE EXISTS (
                    SELECT 1 FROM sgcf.plano_contas_modelo m
                    WHERE m.codigo_gerencial = pcg.codigo_gerencial
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plano_contas_modelo",
                schema: "sgcf");

            migrationBuilder.DropIndex(
                name: "ix_plano_contas_gerencial_tenant_codigo",
                schema: "sgcf",
                table: "plano_contas_gerencial");

            migrationBuilder.DropColumn(
                name: "clonada_de_modelo",
                schema: "sgcf",
                table: "plano_contas_gerencial");

            // Restaura o índice único antigo (sem tenant_id — single-tenant).
            migrationBuilder.CreateIndex(
                name: "IX_plano_contas_gerencial_codigo_gerencial",
                schema: "sgcf",
                table: "plano_contas_gerencial",
                column: "codigo_gerencial",
                unique: true);

            // Nota: os registros do tenant Proxys são preservados no Down — o índice
            // único antigo pode falhar se houver múltiplos tenants com as mesmas contas.
            // Down é para ambientes de desenvolvimento apenas.
        }
    }
}
