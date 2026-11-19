using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.HasIndex(a => new { a.UserId, a.StartDate });

        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
        builder.HasOne(a => a.CoverUser).WithMany().HasForeignKey(a => a.CoverUserId);
        builder.HasOne(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedById);
    }
}
