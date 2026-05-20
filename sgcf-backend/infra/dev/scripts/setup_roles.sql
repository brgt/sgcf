-- Setup inicial de roles para desenvolvimento local.
-- Executado uma vez após criação do banco no Docker Compose.
-- Em produção, este script é executado manualmente pelo DBA antes do primeiro deploy.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_app') THEN
        CREATE ROLE sgcf_app NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sgcf_super') THEN
        CREATE ROLE sgcf_super NOLOGIN BYPASSRLS;
    END IF;
END$$;

-- Concede os roles ao usuário de conexão (sgcf) para que ele possa SET ROLE.
-- Em produção, substituir 'sgcf' pelo usuário de aplicação configurado.
GRANT sgcf_app TO sgcf;
GRANT sgcf_super TO sgcf;
