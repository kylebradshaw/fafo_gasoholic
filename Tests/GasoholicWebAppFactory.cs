using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class GasoholicWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-smoke-secret-xyz";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"gasoholic_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var connStr = $"Data Source={_dbPath}";

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeTestSecret"] = TestSecret,
                ["ConnectionStrings:Sqlite"] = connStr
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the real EF Core DbContext registration with one pointing to the test DB
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connStr);
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Replace the real email sender with a no-op mock
            var emailDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IVerificationEmailSender));
            if (emailDescriptor is not null)
                services.Remove(emailDescriptor);

            services.AddSingleton<IVerificationEmailSender, MockEmailSender>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
                if (File.Exists(_dbPath + "-shm")) File.Delete(_dbPath + "-shm");
                if (File.Exists(_dbPath + "-wal")) File.Delete(_dbPath + "-wal");
            }
            catch { /* best-effort cleanup */ }
        }
        base.Dispose(disposing);
    }
}
