using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Auto> Autos => Set<Auto>();
    public DbSet<Fillup> Fillups => Set<Fillup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
