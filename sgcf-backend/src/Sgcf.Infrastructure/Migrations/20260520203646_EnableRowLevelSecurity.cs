using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sgcf.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        // Tabelas que possuem tenant_id — exatamente as adicionadas pela migration S11.
        // RLS é habilitado nessas tabelas para garantir isolação de dados por tenant.
        private static readonly string[] TabelasTenantScoped =
        [
            "contrato", "parcela", "garantia", "cronograma_pagamento",
            "finimp_detail", "lei4131_detail", "refinimp_detail",
            "nce_detail", "balcao_caixa_detail", "fgi_detail",
            "garantia_cdb_cativo_detail", "garantia_sblc_detail",
            "garantia_aval_detail", "garantia_alienacao_fiduciaria_detail",
            "garantia_duplicatas_detail", "garantia_recebiveis_cartao_detail",
            "garantia_boleto_bancario_detail", "garantia_fgi_detail",
            "cotacao", "proposta", "economia_negociacao",
            "limite_banco",
            "instrumento_hedge", "posicao_snapshot",
            "alerta_vencimento", "alerta_exposicao_banco",
            "simulacao_antecipacao", "simulacao_contratacao", "cenario_simulacao",
            "ebitda_mensal", "snapshot_mensal_posicao",
            "lancamento_contabil", "plano_contas_gerencial",
            "parametro_sistema", "parametro_cotacao",
            "audit_log",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder mb)
        {
            // Criar roles de forma idempotente.
            // sgcf_app: role da aplicação — sujeita ao RLS (isolação por tenant).
            // sgcf_super: role administrativa — BYPASSRLS para operações cross-tenant.
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_app') THEN
                        CREATE ROLE sgcf_app NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_super') THEN
                        CREATE ROLE sgcf_super NOLOGIN BYPASSRLS;
                    END IF;
                END$$;
            ");

            foreach (string tabela in TabelasTenantScoped)
            {
                mb.Sql($"ALTER TABLE sgcf.{tabela} ENABLE ROW LEVEL SECURITY;");

                // FORCE RLS garante que mesmo o owner da tabela (role sgcf) seja filtrado.
                // Essencial para impedir vazamentos de dados quando a conn usa o role sgcf.
                mb.Sql($"ALTER TABLE sgcf.{tabela} FORCE ROW LEVEL SECURITY;");

                // NULLIF(..., '') trata o caso onde app.tenant_id não foi setado:
                // set_config retorna '' quando a variável não existe → NULLIF → NULL →
                // comparação NULL = UUID é false → retorna 0 linhas (fail-safe seguro).
                mb.Sql($@"
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_policies
                            WHERE schemaname = 'sgcf'
                              AND tablename = '{tabela}'
                              AND policyname = 'tenant_isolation'
                        ) THEN
                            CREATE POLICY tenant_isolation ON sgcf.{tabela}
                                AS PERMISSIVE FOR ALL TO sgcf_app
                                USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                        END IF;
                    END$$;
                ");
            }

            // Grants para sgcf_app (aplicação — com RLS aplicado).
            mb.Sql("GRANT USAGE ON SCHEMA sgcf TO sgcf_app;");
            mb.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sgcf TO sgcf_app;");
            mb.Sql("GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA sgcf TO sgcf_app;");

            // Grants para sgcf_super (admin — BYPASSRLS, acesso cross-tenant).
            mb.Sql("GRANT USAGE ON SCHEMA sgcf TO sgcf_super;");
            mb.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sgcf TO sgcf_super;");
            mb.Sql("GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA sgcf TO sgcf_super;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder mb)
        {
            foreach (string tabela in TabelasTenantScoped)
            {
                mb.Sql($"DROP POLICY IF EXISTS tenant_isolation ON sgcf.{tabela};");
                mb.Sql($"ALTER TABLE sgcf.{tabela} DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
