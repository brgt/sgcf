using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration S34 — Snapshot Temporal de Garantias no Contrato.
    ///
    /// O que esta migration faz (Up):
    ///   1. Cria tabela <c>garantia_exigida_revisao</c>.
    ///   2. Cria tabela <c>garantia_exigida_item</c> (sem FK de revisao_id ainda).
    ///   3. Backfill: insere uma revisão inicial por LimiteBanco que tenha itens
    ///      em <c>limite_banco_garantia_exigida</c>.
    ///   4. Copia linhas de <c>limite_banco_garantia_exigida</c> para <c>garantia_exigida_item</c>,
    ///      vinculando cada item à revisão criada no passo 3.
    ///   5. Remove a tabela antiga <c>limite_banco_garantia_exigida</c>.
    ///   6. Adiciona a FK NOT NULL de <c>garantia_exigida_item.revisao_id</c>.
    ///   7. Cria índices e aplica RLS em <c>garantia_exigida_revisao</c>.
    ///   8. Adiciona as 3 colunas de rastreabilidade em <c>contrato</c> com suas FKs e índices.
    ///
    /// ATENÇÃO: Down() é destrutivo. Reverte o schema mas perde todas as revisões
    /// e os snapshots de contrato adicionados depois da migration. Em produção o rollback
    /// deve ser feito reimplantando o binário anterior mantendo esta migration aplicada
    /// (forward-only). Ver SPEC §6.3 e §6.4 para o plano de rollback em produção.
    /// </summary>
    public partial class S34_SnapshotGarantiasContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Passo 1: Criar tabela garantia_exigida_revisao ─────────────────────────────
            migrationBuilder.CreateTable(
                name: "garantia_exigida_revisao",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limite_banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vigencia_inicio = table.Column<Instant>(type: "timestamptz", nullable: false),
                    vigencia_fim = table.Column<Instant>(type: "timestamptz", nullable: true),
                    registrado_em = table.Column<Instant>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "varchar(256)", nullable: true),
                    observacoes = table.Column<string>(type: "varchar(1024)", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantia_exigida_revisao", x => x.id);
                    table.ForeignKey(
                        name: "FK_garantia_exigida_revisao_limite_banco_limite_banco_id",
                        column: x => x.limite_banco_id,
                        principalSchema: "sgcf",
                        principalTable: "limite_banco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Passo 2: Criar tabela garantia_exigida_item sem FK de revisao_id ─────────
            // A FK será adicionada após o backfill, quando revisao_id estiver populado.
            // revisao_id é criado como NOT NULL com DEFAULT temporário para permitir o INSERT;
            // o DEFAULT é removido após o backfill.
            migrationBuilder.Sql(@"
                CREATE TABLE sgcf.garantia_exigida_item (
                    id                     uuid           NOT NULL,
                    revisao_id             uuid           NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                    tipo                   integer        NOT NULL,
                    percentual_sobre_limite numeric(7,4)  NULL,
                    valor_fixo_brl         numeric(20,6)  NULL,
                    obrigatoria            boolean        NOT NULL,
                    observacoes            text           NULL,
                    created_at             timestamptz    NOT NULL,
                    updated_at             timestamptz    NOT NULL,
                    CONSTRAINT ""PK_garantia_exigida_item"" PRIMARY KEY (id),
                    CONSTRAINT ck_garantia_exigida_percentual_intervalo
                        CHECK (percentual_sobre_limite IS NULL OR (percentual_sobre_limite > 0 AND percentual_sobre_limite <= 100)),
                    CONSTRAINT ck_garantia_exigida_percentual_xor_valor
                        CHECK ((percentual_sobre_limite IS NULL AND valor_fixo_brl IS NULL AND tipo = 3)
                            OR (percentual_sobre_limite IS NOT NULL AND valor_fixo_brl IS NULL)
                            OR (percentual_sobre_limite IS NULL AND valor_fixo_brl IS NOT NULL)),
                    CONSTRAINT ck_garantia_exigida_valor_fixo_positivo
                        CHECK (valor_fixo_brl IS NULL OR valor_fixo_brl > 0)
                );
            ");

            // ── Passo 3: Backfill — insere uma revisão inicial por LimiteBanco com itens ─
            // gen_random_uuid() gera UUID v4. Cada LimiteBanco com ao menos 1 item recebe
            // uma revisão inicial com vigencia_inicio = limite_banco.created_at e vigencia_fim = NULL.
            migrationBuilder.Sql(@"
                INSERT INTO sgcf.garantia_exigida_revisao
                    (id, tenant_id, limite_banco_id, vigencia_inicio, vigencia_fim,
                     registrado_em, motivo, observacoes, created_at, updated_at)
                SELECT
                    gen_random_uuid(),
                    lb.tenant_id,
                    lb.id,
                    lb.created_at,
                    NULL,
                    lb.created_at,
                    'Revisão inicial gerada pela migration S34',
                    NULL,
                    lb.created_at,
                    lb.created_at
                FROM sgcf.limite_banco lb
                WHERE EXISTS (
                    SELECT 1
                    FROM sgcf.limite_banco_garantia_exigida gel
                    WHERE gel.limite_banco_id = lb.id
                );
            ");

            // ── Passo 4: Copiar itens para garantia_exigida_item com revisao_id vinculado ─
            // Cada item é ligado à revisão inicial criada para seu limite_banco_id.
            migrationBuilder.Sql(@"
                INSERT INTO sgcf.garantia_exigida_item
                    (id, revisao_id, tipo, percentual_sobre_limite, valor_fixo_brl,
                     obrigatoria, observacoes, created_at, updated_at)
                SELECT
                    gel.id,
                    ger.id,
                    gel.tipo,
                    gel.percentual_sobre_limite,
                    gel.valor_fixo_brl,
                    gel.obrigatoria,
                    gel.observacoes,
                    gel.created_at,
                    gel.updated_at
                FROM sgcf.limite_banco_garantia_exigida gel
                JOIN sgcf.garantia_exigida_revisao ger
                    ON ger.limite_banco_id = gel.limite_banco_id
                   AND ger.vigencia_fim IS NULL;
            ");

            // Remover DEFAULT temporário de revisao_id (já está populado pelo INSERT acima).
            migrationBuilder.Sql(@"
                ALTER TABLE sgcf.garantia_exigida_item
                    ALTER COLUMN revisao_id DROP DEFAULT;
            ");

            // ── Passo 5: Remover a tabela antiga limite_banco_garantia_exigida ─────────────
            migrationBuilder.DropTable(
                name: "limite_banco_garantia_exigida",
                schema: "sgcf");

            // ── Passo 6: Adicionar FK de garantia_exigida_item → garantia_exigida_revisao ──
            migrationBuilder.AddForeignKey(
                name: "FK_garantia_exigida_item_garantia_exigida_revisao_revisao_id",
                schema: "sgcf",
                table: "garantia_exigida_item",
                column: "revisao_id",
                principalSchema: "sgcf",
                principalTable: "garantia_exigida_revisao",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // ── Passo 7: Índices em garantia_exigida_item ─────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_item_revisao_id",
                schema: "sgcf",
                table: "garantia_exigida_item",
                column: "revisao_id");

            migrationBuilder.CreateIndex(
                name: "ux_garantia_exigida_item_revisao_tipo",
                schema: "sgcf",
                table: "garantia_exigida_item",
                columns: new[] { "revisao_id", "tipo" },
                unique: true);

            // ── Passo 8: Índices em garantia_exigida_revisao ──────────────────────────────
            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_revisao_limite_banco",
                schema: "sgcf",
                table: "garantia_exigida_revisao",
                column: "limite_banco_id");

            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_revisao_tenant",
                schema: "sgcf",
                table: "garantia_exigida_revisao",
                column: "tenant_id");

            // Índice único parcial: no máximo uma revisão vigente por (tenant_id, limite_banco_id).
            // Enforça SLB-01 no banco de dados.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_garantia_exigida_revisao_vigente
                    ON sgcf.garantia_exigida_revisao (tenant_id, limite_banco_id)
                    WHERE vigencia_fim IS NULL;
            ");

            // ── Passo 9: RLS em garantia_exigida_revisao ─────────────────────────────────
            // Padrão idêntico ao das demais tabelas tenant-scoped (ex: limite_global_banco).
            // garantia_exigida_item não tem tenant_id próprio — o isolamento é transitivo
            // via JOIN com a revisão pai (isolada por RLS).
            migrationBuilder.Sql(@"
                ALTER TABLE sgcf.garantia_exigida_revisao ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sgcf.garantia_exigida_revisao
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
            ");

            // ── Passo 10: Adicionar 3 colunas de rastreabilidade em contrato ──────────────
            migrationBuilder.AddColumn<Guid>(
                name: "garantias_exigidas_revisao_id",
                schema: "sgcf",
                table: "contrato",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "limite_banco_id",
                schema: "sgcf",
                table: "contrato",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "limite_global_banco_id",
                schema: "sgcf",
                table: "contrato",
                type: "uuid",
                nullable: true);

            // ── Passo 11: Índices parciais nas 3 novas FKs de contrato ───────────────────
            migrationBuilder.CreateIndex(
                name: "IX_contrato_garantias_exigidas_revisao_id",
                schema: "sgcf",
                table: "contrato",
                column: "garantias_exigidas_revisao_id",
                filter: "garantias_exigidas_revisao_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_limite_banco_id",
                schema: "sgcf",
                table: "contrato",
                column: "limite_banco_id",
                filter: "limite_banco_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_limite_global_banco_id",
                schema: "sgcf",
                table: "contrato",
                column: "limite_global_banco_id",
                filter: "limite_global_banco_id IS NOT NULL");

            // ── Passo 12: FKs de contrato com ON DELETE SET NULL ──────────────────────────
            migrationBuilder.AddForeignKey(
                name: "FK_contrato_garantia_exigida_revisao_garantias_exigidas_revisa~",
                schema: "sgcf",
                table: "contrato",
                column: "garantias_exigidas_revisao_id",
                principalSchema: "sgcf",
                principalTable: "garantia_exigida_revisao",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contrato_limite_banco_limite_banco_id",
                schema: "sgcf",
                table: "contrato",
                column: "limite_banco_id",
                principalSchema: "sgcf",
                principalTable: "limite_banco",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contrato_limite_global_banco_limite_global_banco_id",
                schema: "sgcf",
                table: "contrato",
                column: "limite_global_banco_id",
                principalSchema: "sgcf",
                principalTable: "limite_global_banco",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ATENÇÃO: Down() é destrutivo. Todas as revisões são perdidas e os contratos
            // ficam sem as 3 colunas de rastreabilidade. Dados não são recuperados.
            // Usar apenas em desenvolvimento. Em produção: não executar este Down().

            // ── Remover RLS ───────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON sgcf.garantia_exigida_revisao;
                ALTER TABLE sgcf.garantia_exigida_revisao DISABLE ROW LEVEL SECURITY;
            ");

            // ── Remover FKs de contrato ───────────────────────────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_contrato_garantia_exigida_revisao_garantias_exigidas_revisa~",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropForeignKey(
                name: "FK_contrato_limite_banco_limite_banco_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropForeignKey(
                name: "FK_contrato_limite_global_banco_limite_global_banco_id",
                schema: "sgcf",
                table: "contrato");

            // ── Remover índices parciais de contrato ──────────────────────────────────────
            migrationBuilder.DropIndex(
                name: "IX_contrato_garantias_exigidas_revisao_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropIndex(
                name: "IX_contrato_limite_banco_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropIndex(
                name: "IX_contrato_limite_global_banco_id",
                schema: "sgcf",
                table: "contrato");

            // ── Remover 3 colunas de rastreabilidade de contrato ─────────────────────────
            migrationBuilder.DropColumn(
                name: "garantias_exigidas_revisao_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "limite_banco_id",
                schema: "sgcf",
                table: "contrato");

            migrationBuilder.DropColumn(
                name: "limite_global_banco_id",
                schema: "sgcf",
                table: "contrato");

            // ── Remover tabelas novas (PERDA DE DADOS) ────────────────────────────────────
            // DropTable em garantia_exigida_item remove automaticamente a FK de revisao_id
            // e os índices dependentes.
            migrationBuilder.DropTable(
                name: "garantia_exigida_item",
                schema: "sgcf");

            migrationBuilder.DropTable(
                name: "garantia_exigida_revisao",
                schema: "sgcf");

            // ── Recriar tabela antiga limite_banco_garantia_exigida ───────────────────────
            migrationBuilder.CreateTable(
                name: "limite_banco_garantia_exigida",
                schema: "sgcf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    limite_banco_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obrigatoria = table.Column<bool>(type: "boolean", nullable: false),
                    observacoes = table.Column<string>(type: "text", nullable: true),
                    percentual_sobre_limite = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamptz", nullable: false),
                    valor_fixo_brl = table.Column<decimal>(type: "numeric(20,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_banco_garantia_exigida", x => x.id);
                    table.CheckConstraint("ck_garantia_exigida_percentual_intervalo", "percentual_sobre_limite IS NULL OR (percentual_sobre_limite > 0 AND percentual_sobre_limite <= 100)");
                    table.CheckConstraint("ck_garantia_exigida_percentual_xor_valor", "(percentual_sobre_limite IS NULL AND valor_fixo_brl IS NULL AND tipo = 3) OR (percentual_sobre_limite IS NOT NULL AND valor_fixo_brl IS NULL) OR (percentual_sobre_limite IS NULL AND valor_fixo_brl IS NOT NULL)");
                    table.CheckConstraint("ck_garantia_exigida_valor_fixo_positivo", "valor_fixo_brl IS NULL OR valor_fixo_brl > 0");
                    table.ForeignKey(
                        name: "FK_limite_banco_garantia_exigida_limite_banco_limite_banco_id",
                        column: x => x.limite_banco_id,
                        principalSchema: "sgcf",
                        principalTable: "limite_banco",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_garantia_exigida_limite_banco",
                schema: "sgcf",
                table: "limite_banco_garantia_exigida",
                column: "limite_banco_id");

            migrationBuilder.CreateIndex(
                name: "ux_garantia_exigida_limite_tipo",
                schema: "sgcf",
                table: "limite_banco_garantia_exigida",
                columns: new[] { "limite_banco_id", "tipo" },
                unique: true);
        }
    }
}
