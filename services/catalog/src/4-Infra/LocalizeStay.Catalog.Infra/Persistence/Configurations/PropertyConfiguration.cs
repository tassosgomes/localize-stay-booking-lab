using LocalizeStay.Catalog.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Catalog.Infra.Persistence.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("properties");
        builder.HasKey(property => property.Id);

        builder.Property(property => property.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(property => property.Name)
            .HasColumnName("name")
            .HasMaxLength(Property.NameMaxLength)
            .IsRequired();

        builder.Property(property => property.Location)
            .HasColumnName("location")
            .HasMaxLength(Property.LocationMaxLength)
            .IsRequired();

        builder.Property(property => property.HostReferenceId)
            .HasColumnName("host_reference_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(property => property.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .IsRequired()
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<PropertyStatus>(value, ignoreCase: true));
    }
}
