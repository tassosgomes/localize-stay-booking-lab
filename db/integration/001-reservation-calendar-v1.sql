BEGIN;

CREATE OR REPLACE VIEW integration.reservation_calendar_v1 AS
SELECT
    r.id AS reservation_id,
    r.accommodation_id,
    r.check_in,
    r.check_out,
    r.status,
    r.created_at,
    r.terminal_transition_at AS updated_at
FROM booking.reservations AS r
WHERE r.status IN ('confirmada', 'cancelada');

GRANT SELECT ON integration.reservation_calendar_v1 TO catalog_role, booking_role, payment_role;

COMMENT ON VIEW integration.reservation_calendar_v1 IS
    'Snapshot atual das Reservations em estado terminal (confirmada/cancelada); confirmada = ocupação ativa, cancelada = desfecho terminal sem ocupação. Owner: Booking.';
COMMENT ON COLUMN integration.reservation_calendar_v1.reservation_id IS
    'Identificador estável da Reservation publicada.';
COMMENT ON COLUMN integration.reservation_calendar_v1.accommodation_id IS
    'Identificador opaco da Accommodation da Reservation.';
COMMENT ON COLUMN integration.reservation_calendar_v1.check_in IS
    'Início inclusivo do período da Reservation.';
COMMENT ON COLUMN integration.reservation_calendar_v1.check_out IS
    'Fim exclusivo do período da Reservation; o intervalo é [check_in, check_out).';
COMMENT ON COLUMN integration.reservation_calendar_v1.status IS
    'Estado terminal publicado: confirmada ou cancelada.';
COMMENT ON COLUMN integration.reservation_calendar_v1.created_at IS
    'Instante UTC de criação da Reservation.';
COMMENT ON COLUMN integration.reservation_calendar_v1.updated_at IS
    'Instante UTC persistido da transição terminal; não é recalculado na leitura.';

COMMIT;
