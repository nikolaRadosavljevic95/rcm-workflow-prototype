using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class WorkItemEventConfiguration : IEntityTypeConfiguration<WorkItemEvent>
{
    public void Configure(EntityTypeBuilder<WorkItemEvent> builder)
    {
        builder.Property(e => e.Text).HasMaxLength(2000);
        builder.HasIndex(e => new { e.WorkItemId, e.CreatedAt });

        builder.HasOne(e => e.FromUser).WithMany().HasForeignKey(e => e.FromUserId);
        builder.HasOne(e => e.ToUser).WithMany().HasForeignKey(e => e.ToUserId);
        builder.HasOne(e => e.Actor).WithMany().HasForeignKey(e => e.ActorId);
        builder.HasOne(e => e.OnBehalfOf).WithMany().HasForeignKey(e => e.OnBehalfOfId);
    }
}
