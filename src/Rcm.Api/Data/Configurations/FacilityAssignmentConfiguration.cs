using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class FacilityAssignmentConfiguration : IEntityTypeConfiguration<FacilityAssignment>
{
    public void Configure(EntityTypeBuilder<FacilityAssignment> builder)
    {
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.HasOne(a => a.Facility).WithMany().HasForeignKey(a => a.FacilityId);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
        builder.HasOne(a => a.ChangedBy).WithMany().HasForeignKey(a => a.ChangedById);
    }
}
