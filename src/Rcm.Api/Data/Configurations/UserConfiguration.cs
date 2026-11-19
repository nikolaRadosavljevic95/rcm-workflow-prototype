using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rcm.Api.Domain;

namespace Rcm.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(100);
        builder.Property(u => u.Email).HasMaxLength(200);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasOne(u => u.ReportsTo).WithMany().HasForeignKey(u => u.ReportsToId);
    }
}
