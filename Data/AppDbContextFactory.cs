using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var saPassword = Environment.GetEnvironmentVariable("SA_PASSWORD")
            ?? throw new InvalidOperationException("SA_PASSWORD environment variable is not set. Run: export $(grep -v '^#' .env | xargs)");
        var connStr = $"Server=localhost,1433;Database=gasoholic;User Id=sa;Password={saPassword};TrustServerCertificate=True;Encrypt=False;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connStr)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }
}
