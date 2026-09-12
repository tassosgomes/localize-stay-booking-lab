-- 002-roles.sql — cria as roles de serviço (`catalog_role`, `booking_role`,
-- `payment_role`) com LOGIN, de forma idempotente.
--
-- Escopo: provisionamento manual (roles são objetos de cluster, não do database).
-- Executar conectado a qualquer database da instância (ex.: `postgres`).
--
-- Senhas: NUNCA hardcoded neste arquivo versionado. Cada senha chega via
-- variável de substituição do psql (`-v`), normalmente populada a partir de
-- variável de ambiente num wrapper (ver `README.md`):
--
--   CATALOG_PASSWORD=... BOOKING_PASSWORD=... PAYMENT_PASSWORD=... \
--     psql -h <host> -U <admin> -d postgres -v ON_ERROR_STOP=1 \
--       -v catalog_password="$CATALOG_PASSWORD" \
--       -v booking_password="$BOOKING_PASSWORD" \
--       -v payment_password="$PAYMENT_PASSWORD" \
--       -f 002-roles.sql
--
-- Idempotência: o bloco `DO` só cria a role quando ela ainda não existe;
-- o `ALTER ROLE ... WITH LOGIN PASSWORD` subsequente é sempre válido
-- (redefinição convergente) e garante LOGIN mesmo se a role já existia.
-- `ROLE` não suporta `IF NOT EXISTS` nativo — este é o equivalente canônico.

\set ON_ERROR_STOP on

\if :{?catalog_password}
\else
\echo 'ERROR: psql variable "catalog_password" is not set. Pass -v catalog_password="$CATALOG_PASSWORD".'
\quit 1
\endif

\if :{?booking_password}
\else
\echo 'ERROR: psql variable "booking_password" is not set. Pass -v booking_password="$BOOKING_PASSWORD".'
\quit 1
\endif

\if :{?payment_password}
\else
\echo 'ERROR: psql variable "payment_password" is not set. Pass -v payment_password="$PAYMENT_PASSWORD".'
\quit 1
\endif

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'catalog_role') THEN
    CREATE ROLE catalog_role WITH LOGIN;
  END IF;
END
$$;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'booking_role') THEN
    CREATE ROLE booking_role WITH LOGIN;
  END IF;
END
$$;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payment_role') THEN
    CREATE ROLE payment_role WITH LOGIN;
  END IF;
END
$$;

-- `:'var'` interpola a variável já entre aspas como literal SQL seguro
-- (protege contra caracteres especiais; nenhum segredo aparece no arquivo).
ALTER ROLE catalog_role WITH LOGIN PASSWORD :'catalog_password';
ALTER ROLE booking_role WITH LOGIN PASSWORD :'booking_password';
ALTER ROLE payment_role WITH LOGIN PASSWORD :'payment_password';
