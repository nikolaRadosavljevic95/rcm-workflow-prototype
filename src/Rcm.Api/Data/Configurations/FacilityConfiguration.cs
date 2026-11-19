using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.Property(f => f.Code).HasMaxLength(20);
        builder.Property(f => f.Name).HasMaxLength(100);
        builder.HasIndex(f => f.Code).IsUnique();
        builder.HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId);
    }
}
