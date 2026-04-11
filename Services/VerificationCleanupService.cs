using Microsoft.EntityFrameworkCore;

public class VerificationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VerificationCleanupService> _logger;

    public VerificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<VerificationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification cleanup failed");
            }

            // Run once per day
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunCleanupAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-7);

        // Delete stale expired tokens
        var staleTokens = await db.VerificationTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ToListAsync();
        db.VerificationTokens.RemoveRange(staleTokens);

        // Delete unverified users who never clicked their link after 7 days
        var staleUsers = await db.Users
            .Where(u => !u.EmailVerified && u.CreatedAt < cutoff)
            .ToListAsync();
        db.Users.RemoveRange(staleUsers);

        await db.SaveChangesAsync();

        if (staleTokens.Count > 0 || staleUsers.Count > 0)
            _logger.LogInformation("Cleanup: removed {T} tokens, {U} unverified users", staleTokens.Count, staleUsers.Count);
    }
}
