-- verify-reservation-calendar.sql — evidência manual complementar de V-02.
--
-- Executar como superuser depois do bootstrap de schemas/grants e do DDL da view:
--   psql -h <host> -U <admin> -d localize_stay -v ON_ERROR_STOP=1 -f db/integration/verify-reservation-calendar.sql
--
-- O script não faz parte do gate automatizado. Ele valida a relação, o contrato
-- de colunas e os privilégios mínimos; depois exercita as negações via SET ROLE.

\set ON_ERROR_STOP on

DO $$
DECLARE
  role_name text;
  expected_columns text[] := ARRAY[
    'reservation_id', 'accommodation_id', 'check_in', 'check_out', 'status', 'created_at', 'updated_at'
  ];
  actual_columns text[];
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_class relation
    JOIN pg_namespace schema ON schema.oid = relation.relnamespace
    WHERE schema.nspname = 'integration'
      AND relation.relname = 'reservation_calendar_v1'
      AND relation.relkind = 'v'
  ) THEN
    RAISE EXCEPTION 'reservation_calendar_v1 não existe como view em integration';
  END IF;

  SELECT array_agg(column_name ORDER BY ordinal_position)
  INTO actual_columns
  FROM information_schema.columns
  WHERE table_schema = 'integration'
    AND table_name = 'reservation_calendar_v1';

  IF actual_columns IS DISTINCT FROM expected_columns THEN
    RAISE EXCEPTION 'colunas incorretas: esperado %, obtido %', expected_columns, actual_columns;
  END IF;

  FOREACH role_name IN ARRAY ARRAY['catalog_role', 'booking_role', 'payment_role'] LOOP
    IF NOT has_schema_privilege(role_name, 'integration', 'USAGE') THEN
      RAISE EXCEPTION '% sem USAGE em integration', role_name;
    END IF;
    IF has_schema_privilege(role_name, 'integration', 'CREATE') THEN
      RAISE EXCEPTION '% tem CREATE em integration', role_name;
    END IF;
    IF NOT has_table_privilege(role_name, 'integration.reservation_calendar_v1', 'SELECT') THEN
      RAISE EXCEPTION '% sem SELECT em integration.reservation_calendar_v1', role_name;
    END IF;
    IF has_table_privilege(role_name, 'integration.reservation_calendar_v1', 'INSERT, UPDATE, DELETE') THEN
      RAISE EXCEPTION '% tem privilégio de escrita na view', role_name;
    END IF;
    IF has_table_privilege(role_name, 'booking.reservations', 'SELECT') THEN
      RAISE EXCEPTION '% tem SELECT direto em booking.reservations', role_name;
    END IF;
    IF role_name IN ('catalog_role', 'payment_role')
       AND has_schema_privilege(role_name, 'booking', 'USAGE') THEN
      RAISE EXCEPTION '% tem USAGE cruzado em booking', role_name;
    END IF;

    EXECUTE format('SET LOCAL ROLE %I', role_name);
    PERFORM 1 FROM integration.reservation_calendar_v1 LIMIT 1;

    BEGIN
      EXECUTE 'INSERT INTO integration.reservation_calendar_v1 (reservation_id) VALUES (''00000000-0000-0000-0000-000000000001'')';
      RAISE EXCEPTION 'INSERT deveria ser negado para %', role_name;
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
      EXECUTE 'UPDATE integration.reservation_calendar_v1 SET status = status';
      RAISE EXCEPTION 'UPDATE deveria ser negado para %', role_name;
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
      EXECUTE 'DELETE FROM integration.reservation_calendar_v1';
      RAISE EXCEPTION 'DELETE deveria ser negado para %', role_name;
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
      EXECUTE 'CREATE TABLE integration.reservation_calendar_access_probe (id integer)';
      RAISE EXCEPTION 'CREATE deveria ser negado para %', role_name;
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    BEGIN
      EXECUTE 'SELECT * FROM booking.reservations';
      RAISE EXCEPTION 'SELECT direto em booking.reservations deveria ser negado para %', role_name;
    EXCEPTION WHEN insufficient_privilege THEN NULL;
    END;
    EXECUTE 'SET LOCAL ROLE NONE';
  END LOOP;

  RAISE NOTICE 'verify-reservation-calendar: relkind, colunas, grants e negações OK';
END
$$;
