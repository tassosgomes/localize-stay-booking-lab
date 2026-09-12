-- 004-grants.sql — grants mínimos por role + revogação cruzada explícita,
-- de forma idempotente.
--
-- Escopo: provisionamento manual, UMA única vez, após 003 (schemas), dentro
-- do database `localize_stay`.
--
-- Uso:
--   psql -h <host> -U <admin> -d localize_stay -v ON_ERROR_STOP=1 -f 004-grants.sql
--
-- Regra (ownership de dados do baseline):
--   - cada role tem `USAGE, CREATE` apenas no próprio schema;
--   - as três roles têm `USAGE` (schema) + `SELECT` (tabelas/sequências
--     presentes e futuras) no schema `integration`, sem `CREATE` nele;
--   - qualquer acesso cruzado entre schemas de domínio é revogado
--     (ex.: `booking_role` não lê nem escreve em `catalog`).
--
-- Idempotência: `GRANT`/`REVOKE` repetidos convergem sem erro (inclusive
-- `ON ALL TABLES/sequences` com zero objetos e `ALTER DEFAULT PRIVILEGES`).

\set ON_ERROR_STOP on

\c localize_stay

-- Conexão ao database (o owner já tem; PUBLIC mantém o default do template).
GRANT CONNECT ON DATABASE localize_stay TO catalog_role, booking_role, payment_role;

-- Endurece o database: nenhuma role de serviço cria schemas arbitrários;
-- escrita continua possível dentro do próprio schema via USAGE+CREATE abaixo.
REVOKE CREATE ON DATABASE localize_stay FROM catalog_role, booking_role, payment_role;

-- Escrita restrita ao próprio schema.
GRANT USAGE, CREATE ON SCHEMA catalog TO catalog_role;
GRANT USAGE, CREATE ON SCHEMA booking TO booking_role;
GRANT USAGE, CREATE ON SCHEMA payment TO payment_role;

-- Leitura do schema de integração (schema + objetos presentes + futuros).
GRANT USAGE ON SCHEMA integration TO catalog_role, booking_role, payment_role;
GRANT SELECT ON ALL TABLES IN SCHEMA integration TO catalog_role, booking_role, payment_role;
GRANT SELECT, USAGE ON ALL SEQUENCES IN SCHEMA integration TO catalog_role, booking_role, payment_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA integration
  GRANT SELECT ON TABLES TO catalog_role, booking_role, payment_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA integration
  GRANT SELECT, USAGE ON SEQUENCES TO catalog_role, booking_role, payment_role;

-- Revogação cruzada explícita entre schemas de domínio (schema + objetos).
REVOKE ALL ON SCHEMA catalog FROM booking_role, payment_role;
REVOKE ALL ON SCHEMA booking FROM catalog_role, payment_role;
REVOKE ALL ON SCHEMA payment FROM catalog_role, booking_role;

REVOKE ALL ON ALL TABLES IN SCHEMA catalog FROM booking_role, payment_role;
REVOKE ALL ON ALL TABLES IN SCHEMA booking FROM catalog_role, payment_role;
REVOKE ALL ON ALL TABLES IN SCHEMA payment FROM catalog_role, booking_role;

REVOKE ALL ON ALL SEQUENCES IN SCHEMA catalog FROM booking_role, payment_role;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA booking FROM catalog_role, payment_role;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA payment FROM catalog_role, booking_role;

-- Garante que `integration` segue leitura: sem CREATE para as roles de serviço
-- (o USAGE concedido acima é preservado; só CREATE é removido).
REVOKE CREATE ON SCHEMA integration FROM catalog_role, booking_role, payment_role;
