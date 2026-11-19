using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rcm.Api.Domain;

namespace Rcm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityAssignment> FacilityAssignments => Set<FacilityAssignment>();
    public DbSet<ReviewCycle> ReviewCycles => Set<ReviewCycle>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemEvent> WorkItemEvents => Set<WorkItemEvent>();
    public DbSet<Absence> Absences => Set<Absence>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Role>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<CycleStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<WorkItemStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<WorkItemEventType>().HaveConversion<string>().HaveMaxLength(20);

        // SQLite reads DateTime back with Kind Unspecified. Every stored timestamp is UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // History is never deleted, and SQL Server rejects multiple cascade paths to User.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
