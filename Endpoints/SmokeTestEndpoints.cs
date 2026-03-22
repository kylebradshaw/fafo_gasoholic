public static class SmokeTestEndpoints
{
    const string UserIdKey = "userId";

    public static void MapSmokeTestEndpoints(this WebApplication app)
    {
        var secret = app.Configuration["SmokeTestSecret"]
                     ?? Environment.GetEnvironmentVariable("SMOKE_TEST_SECRET");

        // Only register the endpoint if a smoke test secret is configured.
        // This prevents accidental exposure in environments where the secret isn't set.
        if (string.IsNullOrEmpty(secret)) return;

        // POST /auth/dev-login
        // Creates or finds a user, marks EmailVerified = true, establishes a session.
        // Requires X-Smoke-Test-Secret header matching the configured SMOKE_TEST_SECRET.
        app.MapPost("/auth/dev-login", async (
            HttpContext ctx,
            AppDbContext db,
            Microsoft.AspNetCore.Http.IHeaderDictionary _) =>
        {
            var provided = ctx.Request.Headers["X-Smoke-Test-Secret"].ToString();
            if (provided != secret)
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var body = await ctx.Request.ReadFromJsonAsync<DevLoginRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email required" });

            var email = body.Email.Trim().ToLowerInvariant();
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Users, u => u.Email == email);

            if (user is null)
            {
                user = new User { Email = email, EmailVerified = true, CreatedAt = DateTime.UtcNow };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else
            {
                user.EmailVerified = true;
                await db.SaveChangesAsync();
            }

            ctx.Session.SetInt32(UserIdKey, user.Id);
            return Results.Ok(new { status = "ok", email = user.Email });
        });
    }
}

public record DevLoginRequest(string Email);
