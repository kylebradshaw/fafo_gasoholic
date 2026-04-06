using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class GasoholicWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-smoke-secret-xyz";

    // Unique per factory instance so parallel test classes don't share databases
    private readonly string _testDbName = $"gasoholic_test_{Guid.NewGuid():N}";

    private static readonly string SqlServerBase = BuildSqlServerBase();

    private static string BuildSqlServerBase()
    {
        var saPassword = Environment.GetEnvironmentVariable("SA_PASSWORD")
            ?? throw new InvalidOperationException("SA_PASSWORD environment variable is not set. Run: export $(grep -v '^#' .env | xargs)");
        return $"Server=localhost,1433;User Id=sa;Password={saPassword};TrustServerCertificate=True;Encrypt=False;";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeTestSecret"] = TestSecret,
                ["ConnectionStrings:SqlServer"] = $"{SqlServerBase}Database={_testDbName};"
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
                options.UseSqlServer($"{SqlServerBase}Database={_testDbName};");
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Replace the distributed SQL Server cache with an in-memory cache for tests
            var cacheDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
            if (cacheDescriptor is not null)
                services.Remove(cacheDescriptor);

            services.AddDistributedMemoryCache();

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
            // Drop the per-test database on teardown
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer($"{SqlServerBase}Database=master;");
                using var ctx = new AppDbContext(optionsBuilder.Options);
                ctx.Database.ExecuteSqlRaw($"DROP DATABASE IF EXISTS [{_testDbName}]");
            }
            catch { /* best-effort */ }
        }
        base.Dispose(disposing);
    }
}
