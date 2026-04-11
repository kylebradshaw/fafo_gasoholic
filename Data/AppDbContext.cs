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
            b.HasMany(u => u.Autos).WithOne(a => a.User).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
            b.HasMany(u => u.VerificationTokens).WithOne(vt => vt.User).HasForeignKey(vt => vt.UserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Auto>(b =>
        {
            b.ToContainer("Autos");
            b.HasPartitionKey(a => a.UserId);
            b.HasMany(a => a.Fillups).WithOne(f => f.Auto).HasForeignKey(f => f.AutoId).OnDelete(DeleteBehavior.NoAction);
            b.HasMany(a => a.MaintenanceRecords).WithOne(m => m.Auto).HasForeignKey(m => m.AutoId).OnDelete(DeleteBehavior.NoAction);
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
    }
}
