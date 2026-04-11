using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class GasoholicWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-smoke-secret-xyz";

    private readonly string _dbName = $"gasoholic-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeTestSecret"] = TestSecret
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            var emailDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IVerificationEmailSender));
            if (emailDescriptor is not null)
                services.Remove(emailDescriptor);

            services.AddSingleton<IVerificationEmailSender, MockEmailSender>();
        });
    }
}
