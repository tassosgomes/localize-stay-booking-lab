using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalizeStay.Payment.Infra.Persistence;

public sealed class BootstrapCheckConfiguration : IEntityTypeConfiguration<BootstrapCheck>
{
    public void Configure(EntityTypeBuilder<BootstrapCheck> builder)
    {
        builder.ToTable("__bootstrap_check");
        builder.HasKey(check => check.Id);
    }
}
