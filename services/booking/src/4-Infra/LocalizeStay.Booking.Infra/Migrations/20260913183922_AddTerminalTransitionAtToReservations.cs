using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Booking.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminalTransitionAtToReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "terminal_transition_at",
                schema: "booking",
                table: "reservations",
                type: "timestamptz",
                nullable: true);

            // EN-01/ADR-005: solicitada sem timestamp, terminal obrigatoriamente
            // com timestamp. Sem backfill — uma Reservation terminal pré-existente
            // sem timestamp faz esta migration falhar, por decisão da ADR-005.
            migrationBuilder.Sql(
                """
                ALTER TABLE booking.reservations
                    DROP CONSTRAINT IF EXISTS ck_reservations_terminal_transition_at;
                ALTER TABLE booking.reservations
                    ADD CONSTRAINT ck_reservations_terminal_transition_at CHECK (
                        (status = 'solicitada' AND terminal_transition_at IS NULL)
                        OR (status IN ('confirmada', 'cancelada') AND terminal_transition_at IS NOT NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE booking.reservations
                    DROP CONSTRAINT IF EXISTS ck_reservations_terminal_transition_at;
                """);

            migrationBuilder.DropColumn(
                name: "terminal_transition_at",
                schema: "booking",
                table: "reservations");
        }
    }
}
