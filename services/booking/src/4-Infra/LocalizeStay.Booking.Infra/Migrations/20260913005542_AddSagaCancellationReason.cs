using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Booking.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "booking",
                table: "reservation_sagas",
                type: "varchar(500)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "booking",
                table: "reservation_sagas");
        }
    }
}
