using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Booking.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationAndSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__bootstrap_check",
                schema: "booking");

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guest_reference = table.Column<string>(type: "varchar(255)", nullable: false),
                    check_in = table.Column<DateOnly>(type: "date", nullable: false),
                    check_out = table.Column<DateOnly>(type: "date", nullable: false),
                    guests_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "varchar(12)", nullable: false),
                    price_per_night = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "varchar(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reservation_sagas",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "varchar(32)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_sagas", x => x.id);
                    table.ForeignKey(
                        name: "FK_reservation_sagas_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "booking",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_sagas_reservation_id",
                schema: "booking",
                table: "reservation_sagas",
                column: "reservation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_sagas",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "booking");

            migrationBuilder.CreateTable(
                name: "__bootstrap_check",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___bootstrap_check", x => x.Id);
                });
        }
    }
}
