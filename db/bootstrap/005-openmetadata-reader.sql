-- 005-openmetadata-reader.sql — cria o role de leitura `openmetadata_reader`
-- usado pela ingestão de schema do OpenMetadata
-- (scripts/openmetadata/ingestion-postgres.yaml, job catalog-metadata do CI).
--
-- Escopo: provisionamento manual, independente de 001-004 (pode ser rodado a
-- qualquer momento depois deles). Cria o role (objeto de cluster) e concede
-- só CONNECT + USAGE + SELECT nos 4 schemas do projeto — nenhum CREATE, nem
-- em objetos futuros. Sem esse role, o passo "Ingestão de schema Postgres"
-- do job catalog-metadata falha.
--
-- Uso (mesmo padrão de 002-roles.sql: senha via variável de substituição do
-- psql, nunca hardcoded neste arquivo):
--
--   OPENMETADATA_READER_PASSWORD="$(openssl rand -base64 24)" && \
--   psql -h <postgres-main-host> -U <admin> -d postgres -v ON_ERROR_STOP=1 \
--     -v reader_password="$OPENMETADATA_READER_PASSWORD" \
--     -f db/bootstrap/005-openmetadata-reader.sql
--
-- Depois, salve $OPENMETADATA_READER_PASSWORD como o secret
-- OM_POSTGRES_READER_PASSWORD no GitHub (ver scripts/openmetadata/README.md).
--
-- Idempotência: o bloco `DO` só cria o role quando ainda não existe; o
-- `ALTER ROLE ... WITH LOGIN PASSWORD` seguinte sempre converge; `GRANT`/
-- `ALTER DEFAULT PRIVILEGES` repetidos não erram.

\set ON_ERROR_STOP on

\if :{?reader_password}
\else
\echo 'ERROR: psql variable "reader_password" is not set. Pass -v reader_password="$OPENMETADATA_READER_PASSWORD".'
\quit 1
\endif

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'openmetadata_reader') THEN
    CREATE ROLE openmetadata_reader WITH LOGIN;
  END IF;
END
$$;

ALTER ROLE openmetadata_reader WITH LOGIN PASSWORD :'reader_password';

\c localize_stay

GRANT CONNECT ON DATABASE localize_stay TO openmetadata_reader;
REVOKE CREATE ON DATABASE localize_stay FROM openmetadata_reader;

GRANT USAGE ON SCHEMA catalog, booking, payment, integration TO openmetadata_reader;

GRANT SELECT ON ALL TABLES IN SCHEMA catalog, booking, payment, integration TO openmetadata_reader;
GRANT SELECT, USAGE ON ALL SEQUENCES IN SCHEMA catalog, booking, payment, integration TO openmetadata_reader;

ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT SELECT ON TABLES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA booking GRANT SELECT ON TABLES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA payment GRANT SELECT ON TABLES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA integration GRANT SELECT ON TABLES TO openmetadata_reader;

ALTER DEFAULT PRIVILEGES IN SCHEMA catalog GRANT SELECT, USAGE ON SEQUENCES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA booking GRANT SELECT, USAGE ON SEQUENCES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA payment GRANT SELECT, USAGE ON SEQUENCES TO openmetadata_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA integration GRANT SELECT, USAGE ON SEQUENCES TO openmetadata_reader;

-- Sem CREATE em nenhum schema — role é só leitura.
REVOKE CREATE ON SCHEMA catalog, booking, payment, integration FROM openmetadata_reader;
