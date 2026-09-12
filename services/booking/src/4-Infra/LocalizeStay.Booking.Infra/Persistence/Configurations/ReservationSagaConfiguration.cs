using LocalizeStay.Booking.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Booking.Infra.Persistence.Configurations;

public sealed class ReservationSagaConfiguration : IEntityTypeConfiguration<ReservationSaga>
{
    public void Configure(EntityTypeBuilder<ReservationSaga> builder)
    {
        builder.ToTable("reservation_sagas");
        builder.HasKey(saga => saga.Id);
        builder.Property(saga => saga.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(saga => saga.ReservationId).HasColumnName("reservation_id");
        builder.Property(saga => saga.CorrelationId).HasColumnName("correlation_id");
        builder.Property(saga => saga.State).HasColumnName("state")
            .HasColumnType("varchar(32)")
            .HasConversion<string>();
        builder.Property(saga => saga.CreatedAt).HasColumnName("created_at");
    }
}
