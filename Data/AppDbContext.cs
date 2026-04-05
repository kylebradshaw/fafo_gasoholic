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
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasMaxLength(256);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Auto>()
            .HasOne(a => a.User)
            .WithMany(u => u.Autos)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Fillup>()
            .HasOne(f => f.Auto)
            .WithMany(a => a.Fillups)
            .HasForeignKey(f => f.AutoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Fillup>()
            .Property(f => f.FuelType)
            .HasConversion<string>();

        modelBuilder.Entity<MaintenanceRecord>()
            .HasOne(m => m.Auto)
            .WithMany(a => a.MaintenanceRecords)
            .HasForeignKey(m => m.AutoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MaintenanceRecord>()
            .Property(m => m.Type)
            .HasConversion<string>();

        modelBuilder.Entity<VerificationToken>()
            .HasOne(vt => vt.User)
            .WithMany(u => u.VerificationTokens)
            .HasForeignKey(vt => vt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VerificationToken>()
            .HasIndex(vt => vt.Token)
            .IsUnique();
    }
}
