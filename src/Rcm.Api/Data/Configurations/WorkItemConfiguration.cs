using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.Property(w => w.BlockedReason).HasMaxLength(500);
        builder.Property(w => w.Summary).HasMaxLength(2000);
        builder.Property(w => w.Version).IsConcurrencyToken();

        builder.HasIndex(w => new { w.CycleId, w.FacilityId }).IsUnique();
        builder.HasIndex(w => new { w.AssigneeId, w.CycleId });

        builder.HasOne(w => w.Cycle).WithMany().HasForeignKey(w => w.CycleId);
        builder.HasOne(w => w.Facility).WithMany().HasForeignKey(w => w.FacilityId);
        builder.HasOne(w => w.Assignee).WithMany().HasForeignKey(w => w.AssigneeId);
        builder.HasOne(w => w.SubmittedBy).WithMany().HasForeignKey(w => w.SubmittedById);
        builder.HasOne(w => w.ApprovedBy).WithMany().HasForeignKey(w => w.ApprovedById);
        builder.HasOne(w => w.PreviousItem).WithOne().HasForeignKey<WorkItem>(w => w.PreviousItemId);
        builder.HasMany(w => w.Events).WithOne(e => e.WorkItem).HasForeignKey(e => e.WorkItemId);
    }
}
