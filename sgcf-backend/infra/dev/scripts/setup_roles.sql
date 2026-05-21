-- Setup inicial de roles para desenvolvimento local.
-- Executado uma vez após criação do banco no Docker Compose.
-- Em produção, este script é executado manualmente pelo DBA antes do primeiro deploy.
--
-- ATENÇÃO — separação de responsabilidades:
--   sgcf_app   : role sem BYPASSRLS — usada pela aplicação em runtime (RLS ativo).
--   sgcf_super : role com BYPASSRLS — usada SOMENTE para provisionamento admin e migrations.
--
-- Em produção, criar dois usuários de login DISTINTOS:
--   sgcf_runtime  → recebe GRANT sgcf_app  (connection string da API/Jobs)
--   sgcf_admin    → recebe GRANT sgcf_super (connection string do provisioner/migrations)
--
-- NUNCA conceder ambos os roles ao mesmo usuário de login em produção.
-- O usuário de runtime não deve poder fazer SET ROLE sgcf_super.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_app') THEN
        CREATE ROLE sgcf_app NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_super') THEN
        CREATE ROLE sgcf_super NOLOGIN BYPASSRLS;
    END IF;
END$$;

-- DEV ONLY: usuário único 'sgcf' recebe ambos os roles por conveniência local.
-- NÃO replicar em staging ou produção — ver comentário acima.
GRANT sgcf_app TO sgcf;
GRANT sgcf_super TO sgcf;
