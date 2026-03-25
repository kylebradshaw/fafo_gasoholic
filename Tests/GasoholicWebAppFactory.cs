using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class GasoholicWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-smoke-secret-xyz";

    // Unique per factory instance so parallel test classes don't share databases
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"gasoholic-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeTestSecret"] = TestSecret,
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
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
                options.UseSqlite($"Data Source={_dbPath}");
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
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
