using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S40_CotacaoTenorEDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Campos de domínio opcionais (nullable, retrocompatíveis) — SPEC S40 §2.3–§2.6 ──
            migrationBuilder.AddColumn<string>(
                name: "banco_repassador_pretendido",
                schema: "sgcf",
                table: "cotacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "carencia_meses",
                schema: "sgcf",
                table: "cotacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "finalidade_bndes",
                schema: "sgcf",
                table: "cotacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "indexador_percentual_cdi",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "indexador_spread_aa",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "indexador_taxa_prefixada_aa",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "indexador_tipo",
                schema: "sgcf",
                table: "cotacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentual_cobertura_fgi",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ptax_usada",
                schema: "sgcf",
                table: "cotacao",
                type: "numeric(12,6)",
                nullable: true);

            // ── Tenor + moeda alvo: adicionar como NULLABLE para backfill em linhas existentes ──
            migrationBuilder.AddColumn<int>(
                name: "prazo_maximo_valor",
                schema: "sgcf",
                table: "cotacao",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prazo_maximo_unidade",
                schema: "sgcf",
                table: "cotacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moeda_alvo",
                schema: "sgcf",
                table: "cotacao",
                type: "text",
                nullable: true);

            // ── Backfill da intenção do operador a partir dos campos canônicos (linhas legadas) ──
            // Observação de produção: sob FORCE ROW LEVEL SECURITY, executar a migração com papel
            // BYPASSRLS/superusuário para que o UPDATE alcance todas as linhas (SPEC S40 §12).
            migrationBuilder.Sql(@"
                UPDATE sgcf.cotacao
                   SET prazo_maximo_valor   = COALESCE(prazo_maximo_valor, prazo_maximo_dias),
                       prazo_maximo_unidade = COALESCE(prazo_maximo_unidade, 'Dias'),
                       ptax_usada           = COALESCE(ptax_usada, ptax_usada_usd_brl),
                       moeda_alvo           = COALESCE(
                                                  moeda_alvo,
                                                  CASE WHEN modalidade IN ('FINIMP', 'REFINIMP', 'LEI_4131')
                                                       THEN 'USD' ELSE 'BRL' END);");

            // ── Promover tenor e moeda alvo a NOT NULL após o backfill ──
            migrationBuilder.Sql(@"
                ALTER TABLE sgcf.cotacao
                    ALTER COLUMN prazo_maximo_valor   SET NOT NULL,
                    ALTER COLUMN prazo_maximo_unidade SET NOT NULL,
                    ALTER COLUMN moeda_alvo           SET NOT NULL;");

            // ── Restrições de domínio (rede de segurança no banco) — SPEC S40 §7 ──
            migrationBuilder.Sql(@"
                ALTER TABLE sgcf.cotacao
                    ADD CONSTRAINT cotacao_prazo_unidade_chk
                        CHECK (prazo_maximo_unidade IN ('Dias', 'Meses')),
                    ADD CONSTRAINT cotacao_prazo_valor_chk
                        CHECK (prazo_maximo_valor >= 1),
                    ADD CONSTRAINT cotacao_carencia_meses_chk
                        CHECK (carencia_meses IS NULL OR carencia_meses >= 0),
                    ADD CONSTRAINT cotacao_cobertura_fgi_chk
                        CHECK (percentual_cobertura_fgi IS NULL
                               OR (percentual_cobertura_fgi >= 0 AND percentual_cobertura_fgi <= 100));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE sgcf.cotacao
                    DROP CONSTRAINT IF EXISTS cotacao_prazo_unidade_chk,
                    DROP CONSTRAINT IF EXISTS cotacao_prazo_valor_chk,
                    DROP CONSTRAINT IF EXISTS cotacao_carencia_meses_chk,
                    DROP CONSTRAINT IF EXISTS cotacao_cobertura_fgi_chk;");

            migrationBuilder.DropColumn(name: "banco_repassador_pretendido", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "carencia_meses", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "finalidade_bndes", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "indexador_percentual_cdi", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "indexador_spread_aa", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "indexador_taxa_prefixada_aa", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "indexador_tipo", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "moeda_alvo", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "percentual_cobertura_fgi", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "prazo_maximo_unidade", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "prazo_maximo_valor", schema: "sgcf", table: "cotacao");
            migrationBuilder.DropColumn(name: "ptax_usada", schema: "sgcf", table: "cotacao");
        }
    }
}
