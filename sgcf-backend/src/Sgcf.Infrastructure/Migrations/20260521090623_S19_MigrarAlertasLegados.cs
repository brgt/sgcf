using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class S19_MigrarAlertasLegados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migração idempotente de alertas legados para a tabela unificada sgcf.alertas.
            //
            // Bloco 1: alerta_vencimento → alertas
            //   - categoria = 1 (Vencimento)
            //   - severidade depende de tipo_alerta: D_MENOS_0 → 1 (Critico), D_MENOS_3 → 2 (Atencao), D_MENOS_7 → 3 (Informativo)
            //   - chave_idempotencia = 'legado-vencimento:' || id (preserva unicidade sem colisão com novas regras)
            //   - status = 1 (Aberto)
            //   - perfis: Tesouraria (3) e GerenteFinanceiro (2)
            //
            // Bloco 2: alerta_exposicao_banco → alertas
            //   - categoria = 4 (LimiteBanco)
            //   - severidade = 2 (Atencao) — limiar original era 80%
            //   - chave_idempotencia = 'legado-exposicao:' || id
            //   - perfis: GerenteFinanceiro (2) e Cfo (1)
            //
            // Condição DO_NOTHING ON CONFLICT: reexecutar a migration não cria duplicatas.
            // Verificação de existência da tabela via information_schema: seguro se tabelas foram removidas.

            migrationBuilder.Sql(@"
DO $$
BEGIN

-- Bloco 1: migrar alerta_vencimento
IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'sgcf' AND table_name = 'alerta_vencimento'
) THEN

    INSERT INTO sgcf.alertas (
        id, tenant_id, categoria, severidade,
        titulo, descricao,
        origem_tipo, origem_id,
        acao_rotulo, acao_rota,
        status, criado_em, expira_em, chave_idempotencia
    )
    SELECT
        av.id,
        COALESCE(av.tenant_id, '00000000-0000-0000-0000-000000000000'::uuid),
        1 AS categoria,   -- Vencimento
        CASE av.tipo_alerta
            WHEN 'D_MENOS_0' THEN 1  -- Critico
            WHEN 'D_MENOS_3' THEN 2  -- Atencao
            ELSE 3                   -- Informativo (D_MENOS_7 e outros)
        END AS severidade,
        'Vencimento legado — ' || av.tipo_alerta AS titulo,
        'Vencimento iminente do contrato ' || av.contrato_id::text ||
            ' em ' || av.data_vencimento::text ||
            ' (migrado de alerta_vencimento).' AS descricao,
        'EventoCronograma' AS origem_tipo,
        NULL AS origem_id,
        'Ver contrato' AS acao_rotulo,
        '/contratos/' || av.contrato_id::text AS acao_rota,
        1 AS status,  -- Aberto
        av.criado_em,
        NULL AS expira_em,
        'legado-vencimento:' || av.id::text AS chave_idempotencia
    FROM sgcf.alerta_vencimento av
    ON CONFLICT (chave_idempotencia) DO NOTHING;

    -- Perfis para os alertas de vencimento recém-inseridos (Tesouraria=3, GerenteFinanceiro=2)
    INSERT INTO sgcf.alerta_perfil_visivel (alerta_id, perfil)
    SELECT a.id, perfil_val
    FROM sgcf.alertas a
    CROSS JOIN (VALUES (2), (3)) AS perfis(perfil_val)
    WHERE a.chave_idempotencia LIKE 'legado-vencimento:%'
      AND NOT EXISTS (
          SELECT 1 FROM sgcf.alerta_perfil_visivel apv
          WHERE apv.alerta_id = a.id AND apv.perfil = perfil_val
      );

END IF;

-- Bloco 2: migrar alerta_exposicao_banco
IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'sgcf' AND table_name = 'alerta_exposicao_banco'
) THEN

    INSERT INTO sgcf.alertas (
        id, tenant_id, categoria, severidade,
        titulo, descricao,
        origem_tipo, origem_id,
        acao_rotulo, acao_rota,
        status, criado_em, expira_em, chave_idempotencia
    )
    SELECT
        aeb.id,
        COALESCE(aeb.tenant_id, '00000000-0000-0000-0000-000000000000'::uuid),
        4 AS categoria,   -- LimiteBanco
        2 AS severidade,  -- Atencao (limiar original era 80%)
        'Exposição de banco elevada (legado)' AS titulo,
        'Banco ' || aeb.banco_id::text ||
            ' com ' || ROUND(aeb.percentual_ocupacao * 100, 1)::text || '% de ocupação em ' ||
            aeb.data_alerta::text ||
            ' (migrado de alerta_exposicao_banco).' AS descricao,
        'Banco' AS origem_tipo,
        aeb.banco_id AS origem_id,
        'Ver limites' AS acao_rotulo,
        '/bancos/' || aeb.banco_id::text AS acao_rota,
        1 AS status,  -- Aberto
        aeb.criado_em,
        NULL AS expira_em,
        'legado-exposicao:' || aeb.id::text AS chave_idempotencia
    FROM sgcf.alerta_exposicao_banco aeb
    ON CONFLICT (chave_idempotencia) DO NOTHING;

    -- Perfis para os alertas de exposição recém-inseridos (GerenteFinanceiro=2, Cfo=1)
    INSERT INTO sgcf.alerta_perfil_visivel (alerta_id, perfil)
    SELECT a.id, perfil_val
    FROM sgcf.alertas a
    CROSS JOIN (VALUES (1), (2)) AS perfis(perfil_val)
    WHERE a.chave_idempotencia LIKE 'legado-exposicao:%'
      AND NOT EXISTS (
          SELECT 1 FROM sgcf.alerta_perfil_visivel apv
          WHERE apv.alerta_id = a.id AND apv.perfil = perfil_val
      );

END IF;

END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove os alertas migrados das tabelas legadas.
            // Seguro: afeta apenas chaves prefixadas com 'legado-'.
            migrationBuilder.Sql(@"
DELETE FROM sgcf.alerta_perfil_visivel
WHERE alerta_id IN (
    SELECT id FROM sgcf.alertas
    WHERE chave_idempotencia LIKE 'legado-vencimento:%'
       OR chave_idempotencia LIKE 'legado-exposicao:%'
);

DELETE FROM sgcf.alertas
WHERE chave_idempotencia LIKE 'legado-vencimento:%'
   OR chave_idempotencia LIKE 'legado-exposicao:%';
");
        }
    }
}
