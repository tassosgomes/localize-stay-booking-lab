-- 003-schemas.sql — cria os schemas `catalog`, `booking`, `payment` e
-- `integration` dentro do database `localize_stay`, de forma idempotente.
--
-- Escopo: provisionamento manual, UMA única vez, após 001 (database) e 002
-- (roles). Requer conexão com privilégio suficiente dentro de `localize_stay`.
--
-- Uso:
--   psql -h <host> -U <admin> -d localize_stay -v ON_ERROR_STOP=1 -f 003-schemas.sql
--
-- Ownership:
--   - `catalog` → `catalog_role`, `booking` → `booking_role`,
--     `payment` → `payment_role` (owner = role do próprio domínio).
--   - `integration` NÃO tem role de escrita própria nesta fase (nenhum
--     consumidor escreve nele): o owner permanece o operador de bootstrap
--     (ex.: `postgres`); nenhuma role de serviço é owner dele.
--
-- Idempotência: `CREATE SCHEMA IF NOT EXISTS` não altera o owner quando o
-- schema já existe, por isso cada `CREATE` é seguido de `ALTER SCHEMA ...
-- OWNER TO ...` convergente (sem erro quando o owner já está correto).

\set ON_ERROR_STOP on

\c localize_stay

CREATE SCHEMA IF NOT EXISTS catalog AUTHORIZATION catalog_role;
CREATE SCHEMA IF NOT EXISTS booking AUTHORIZATION booking_role;
CREATE SCHEMA IF NOT EXISTS payment AUTHORIZATION payment_role;
CREATE SCHEMA IF NOT EXISTS integration;

ALTER SCHEMA catalog OWNER TO catalog_role;
ALTER SCHEMA booking OWNER TO booking_role;
ALTER SCHEMA payment OWNER TO payment_role;
-- `integration` propositalmente sem ALTER OWNER para role de serviço.
