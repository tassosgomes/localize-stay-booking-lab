using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Booking.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRequestSentAtToReservationSagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "payment_request_sent_at",
                schema: "booking",
                table: "reservation_sagas",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_request_sent_at",
                schema: "booking",
                table: "reservation_sagas");
        }
    }
}
