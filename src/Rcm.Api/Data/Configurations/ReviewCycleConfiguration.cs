using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class ReviewCycleConfiguration : IEntityTypeConfiguration<ReviewCycle>
{
    public void Configure(EntityTypeBuilder<ReviewCycle> builder)
    {
        builder.HasIndex(c => new { c.Year, c.Month }).IsUnique();

        // At most one open month. The filter is plain SQL that SQLite and SQL Server both accept.
        builder.HasIndex(c => c.Status).IsUnique().HasFilter("Status = 'Open'");

        builder.HasOne(c => c.OpenedBy).WithMany().HasForeignKey(c => c.OpenedById);
        builder.HasOne(c => c.ClosedBy).WithMany().HasForeignKey(c => c.ClosedById);
    }
}
