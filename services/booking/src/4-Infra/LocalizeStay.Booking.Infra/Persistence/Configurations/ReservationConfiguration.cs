using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Booking.Infra.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(reservation => reservation.AccommodationId).HasColumnName("accommodation_id");
        builder.Property(reservation => reservation.GuestReference).HasColumnName("guest_reference")
            .HasColumnType("varchar(255)").IsRequired();
        builder.Property(reservation => reservation.CheckIn).HasColumnName("check_in");
        builder.Property(reservation => reservation.CheckOut).HasColumnName("check_out");
        builder.Property(reservation => reservation.GuestsCount).HasColumnName("guests_count");
        builder.Property(reservation => reservation.Status).HasColumnName("status")
            .HasColumnType("varchar(12)")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<ReservationStatus>(value, ignoreCase: true));
        builder.Property(reservation => reservation.PricePerNight).HasColumnName("price_per_night")
            .HasColumnType("numeric(10,2)");
        builder.Property(reservation => reservation.TotalAmount).HasColumnName("total_amount")
            .HasColumnType("numeric(10,2)");
        builder.Property(reservation => reservation.Currency).HasColumnName("currency")
            .HasColumnType("varchar(3)").IsRequired();
        builder.Property(reservation => reservation.CreatedAt).HasColumnName("created_at");

        builder.HasOne(reservation => reservation.Saga)
            .WithOne()
            .HasForeignKey<ReservationSaga>(saga => saga.ReservationId);
    }
}
