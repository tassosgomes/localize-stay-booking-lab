-- 001-database.sql — cria o database `localize_stay` de forma idempotente.
--
-- Escopo: provisionamento manual, executado UMA única vez por um operador com
-- privilégio suficiente (CREATEDB ou superuser) contra a instância Postgres 16
-- (`postgres-main` em produção; container descartável no gate/validação).
--
-- Uso:
--   psql -h <host> -U <admin> -d postgres -v ON_ERROR_STOP=1 -f 001-database.sql
--
-- Idempotência: o `SELECT ... WHERE NOT EXISTS ... \gexec` só emite o
-- `CREATE DATABASE` quando o database ainda não existe; a segunda execução
-- retorna zero linhas e não faz nada (sem erro). `CREATE DATABASE` não suporta
-- `IF NOT EXISTS` nativo — este é o equivalente canônico via psql.
-- Não contém credenciais.

\set ON_ERROR_STOP on

SELECT 'CREATE DATABASE localize_stay ENCODING ''UTF8'''
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'localize_stay')\gexec
