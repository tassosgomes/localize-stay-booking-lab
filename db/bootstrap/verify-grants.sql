-- verify-grants.sql — verificação determinística dos grants exatos por role.
--
-- Escopo: leitura (não altera nada). Executar como operador com privilégio
-- suficiente (ex.: superuser) após a SEGUNDA rodada dos 4 scripts, dentro de
-- `localize_stay`:
--
--   psql -h <host> -U <admin> -d localize_stay -v ON_ERROR_STOP=1 -f verify-grants.sql
--
-- O que prova:
--   1. matriz legível role × schema (`USAGE`/`CREATE` via `has_schema_privilege`);
--   2. bloco `DO` que falha com `RAISE EXCEPTION` em qualquer divergência:
--      cada role tem USAGE+CREATE no próprio schema, USAGE (sem CREATE) em
--      `integration`, e NENHUM privilégio (USAGE ou CREATE) nos schemas dos
--      outros domínios;
--   3. nível de tabela em `integration`: se já houver tabelas, exige SELECT
--      para as três roles (`has_table_privilege`); se ainda não houver
--      (caso da Fase 0), registra NOTICE — o SELECT futuro está coberto pelo
--      `GRANT ON ALL TABLES` + `ALTER DEFAULT PRIVILEGES` de 004-grants.sql.
-- A negação funcional (ex.: `booking_role` escrever em `catalog` falha com
-- `permission denied`) é conferida à parte com `SET ROLE` (ver README).

\set ON_ERROR_STOP on

\c localize_stay

-- 1. Matriz legível role × schema.
SELECT
  r.rolname AS role,
  n.nspname AS schema,
  has_schema_privilege(r.rolname, n.nspname, 'USAGE') AS usage,
  has_schema_privilege(r.rolname, n.nspname, 'CREATE') AS create_priv
FROM (VALUES ('catalog_role'), ('booking_role'), ('payment_role')) AS r(rolname)
CROSS JOIN (VALUES ('catalog'), ('booking'), ('payment'), ('integration')) AS n(nspname)
ORDER BY 1, 2;

-- 2. Asserções rígidas: qualquer divergência aborta com exceção.
DO $$
DECLARE
  rec record;
BEGIN
  -- 2a. Cada role tem USAGE+CREATE no próprio schema.
  IF NOT (has_schema_privilege('catalog_role', 'catalog', 'USAGE')
          AND has_schema_privilege('catalog_role', 'catalog', 'CREATE')) THEN
    RAISE EXCEPTION 'grant incorreto: catalog_role sem USAGE+CREATE em catalog';
  END IF;
  IF NOT (has_schema_privilege('booking_role', 'booking', 'USAGE')
          AND has_schema_privilege('booking_role', 'booking', 'CREATE')) THEN
    RAISE EXCEPTION 'grant incorreto: booking_role sem USAGE+CREATE em booking';
  END IF;
  IF NOT (has_schema_privilege('payment_role', 'payment', 'USAGE')
          AND has_schema_privilege('payment_role', 'payment', 'CREATE')) THEN
    RAISE EXCEPTION 'grant incorreto: payment_role sem USAGE+CREATE em payment';
  END IF;

  -- 2b. As três roles têm USAGE (sem CREATE) em `integration`.
  FOR rec IN SELECT unnest(ARRAY['catalog_role', 'booking_role', 'payment_role']) AS role LOOP
    IF NOT has_schema_privilege(rec.role, 'integration', 'USAGE') THEN
      RAISE EXCEPTION 'grant incorreto: % sem USAGE em integration', rec.role;
    END IF;
    IF has_schema_privilege(rec.role, 'integration', 'CREATE') THEN
      RAISE EXCEPTION 'grant incorreto: % com CREATE em integration (deveria ser só leitura)', rec.role;
    END IF;
  END LOOP;

  -- 2c. Nenhum acesso cruzado entre schemas de domínio (USAGE e CREATE).
  FOR rec IN SELECT * FROM (VALUES
    ('catalog_role', 'booking'),
    ('catalog_role', 'payment'),
    ('booking_role', 'catalog'),
    ('booking_role', 'payment'),
    ('payment_role', 'catalog'),
    ('payment_role', 'booking')
  ) AS t(role, schema) LOOP
    IF has_schema_privilege(rec.role, rec.schema, 'USAGE') THEN
      RAISE EXCEPTION 'grant incorreto: % com USAGE em % (acesso cruzado)', rec.role, rec.schema;
    END IF;
    IF has_schema_privilege(rec.role, rec.schema, 'CREATE') THEN
      RAISE EXCEPTION 'grant incorreto: % com CREATE em % (acesso cruzado)', rec.role, rec.schema;
    END IF;
  END LOOP;

  RAISE NOTICE 'verify-grants: grants de schema OK (próprio=USAGE+CREATE, integration=USAGE sem CREATE, sem acesso cruzado)';
END
$$;

-- 3. Nível de tabela em `integration` (SELECT para as três roles, se houver tabelas).
DO $$
DECLARE
  tbl record;
  role_name text;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'integration') THEN
    RAISE NOTICE 'verify-grants: integration ainda sem tabelas — SELECT futuro coberto por 004-grants.sql (GRANT ON ALL TABLES + DEFAULT PRIVILEGES)';
    RETURN;
  END IF;
  FOR tbl IN SELECT tablename FROM pg_tables WHERE schemaname = 'integration' LOOP
    FOREACH role_name IN ARRAY ARRAY['catalog_role', 'booking_role', 'payment_role'] LOOP
      IF NOT has_table_privilege(role_name, 'integration.' || quote_ident(tbl.tablename), 'SELECT') THEN
        RAISE EXCEPTION 'grant incorreto: % sem SELECT em integration.%', role_name, tbl.tablename;
      END IF;
    END LOOP;
  END LOOP;
  RAISE NOTICE 'verify-grants: SELECT em integration OK para as três roles';
END
$$;
