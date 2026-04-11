using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Auto> Autos => Set<Auto>();
    public DbSet<Fillup> Fillups => Set<Fillup>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToContainer("Users");
            b.HasPartitionKey(u => u.Id);
            b.Property(u => u.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<Auto>(b =>
        {
            b.ToContainer("Autos");
            b.HasPartitionKey(a => a.UserId);
        });

        modelBuilder.Entity<Fillup>(b =>
        {
            b.ToContainer("Fillups");
            b.HasPartitionKey(f => f.AutoId);
            b.Property(f => f.FuelType).HasConversion<string>();
        });

        modelBuilder.Entity<MaintenanceRecord>(b =>
        {
            b.ToContainer("Maintenance");
            b.HasPartitionKey(m => m.AutoId);
            b.Property(m => m.Type).HasConversion<string>();
        });

        modelBuilder.Entity<VerificationToken>(b =>
        {
            b.ToContainer("VerificationTokens");
            b.HasPartitionKey(vt => vt.UserId);
            b.HasDefaultTimeToLive(7 * 24 * 60 * 60);
        });

        // All Cosmos document properties are camelCase. See NAMING.md for rationale.
        // Enforced by Tests/NamingConventionTests.cs.
        ApplyCamelCaseJsonPropertyNames(modelBuilder);
    }

    static void ApplyCamelCaseJsonPropertyNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.IsShadowProperty()) continue;
                var current = prop.GetJsonPropertyName();
                if (string.IsNullOrEmpty(current)) continue;
                // Skip Cosmos system properties (`id`, `_etag`, `_ts`, `__type`, etc.)
                if (current == "id" || current.StartsWith('_')) continue;
                if (char.IsUpper(current[0]))
                    prop.SetJsonPropertyName(char.ToLowerInvariant(current[0]) + current[1..]);
            }
        }
    }
}
