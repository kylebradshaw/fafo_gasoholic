using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    const string UserIdKey = "userId";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest req, AppDbContext db, IVerificationEmailSender emailSender, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required" });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users
                .Include(u => u.VerificationTokens)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user is null)
            {
                user = new User { Email = email, EmailVerified = false, CreatedAt = DateTime.UtcNow };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            if (user.EmailVerified)
            {
                ctx.Session.SetInt32(UserIdKey, user.Id);
                return Results.Ok(new { status = "ok", email = user.Email });
            }

            // Unverified: check for an active (unused, non-expired) token
            var hasActiveToken = user.VerificationTokens
                .Any(t => t.UsedAt is null && t.ExpiresAt > DateTime.UtcNow);

            if (hasActiveToken)
                return Results.Accepted(null, new { status = "pending" });

            // Generate and send new magic link
            var token = Guid.NewGuid().ToString("N");
            db.VerificationTokens.Add(new VerificationToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();

            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            await emailSender.SendMagicLinkAsync(user.Email, token, baseUrl);

            return Results.Accepted(null, new { status = "pending" });
        });

        app.MapGet("/auth/verify", async (string token, AppDbContext db, HttpContext ctx) =>
        {
            var vt = await db.VerificationTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token);

            if (vt is null)
                return Results.BadRequest(new { error = "token_not_found" });
            if (vt.UsedAt is not null)
                return Results.BadRequest(new { error = "token_used" });
            if (vt.ExpiresAt <= DateTime.UtcNow)
                return Results.BadRequest(new { error = "token_expired" });

            vt.UsedAt = DateTime.UtcNow;
            vt.User.EmailVerified = true;
            await db.SaveChangesAsync();

            ctx.Session.SetInt32(UserIdKey, vt.User.Id);
            return Results.Redirect("/app.html");
        });

        app.MapPost("/auth/resend", async (ResendRequest req, AppDbContext db, IVerificationEmailSender emailSender, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required" });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null || user.EmailVerified)
                return Results.Ok(new { status = "ok" });  // Don't reveal whether user exists

            var cutoff = DateTime.UtcNow.AddHours(-1);
            var recentCount = await db.VerificationTokens
                .CountAsync(t => t.UserId == user.Id && t.CreatedAt >= cutoff);

            if (recentCount >= 4)  // login + 3 resends = 4 total tokens before lockout
                return Results.Json(new { error = "too_many_requests" }, statusCode: 429);

            // Invalidate old unused tokens (mark as used so they're still counted in rate limit)
            await db.VerificationTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow));

            var token = Guid.NewGuid().ToString("N");
            db.VerificationTokens.Add(new VerificationToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();

            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            await emailSender.SendMagicLinkAsync(user.Email, token, baseUrl);

            return Results.Accepted(null, new { status = "pending" });
        });

        app.MapPost("/auth/logout", (HttpContext ctx) =>
        {
            ctx.Session.Clear();
            return Results.Ok();
        });

        app.MapGet("/auth/me", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.Session.GetInt32(UserIdKey);
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId.Value);
            if (user is null) return Results.Unauthorized();
            if (!user.EmailVerified) return Results.Json(new { error = "unverified" }, statusCode: 403);

            return Results.Ok(new { email = user.Email });
        });
    }

    public static int? GetUserId(this HttpContext ctx) =>
        ctx.Session.GetInt32(UserIdKey);

    public static async Task<IResult?> RequireAuth(HttpContext ctx, AppDbContext db)
    {
        var userId = ctx.Session.GetInt32(UserIdKey);
        if (userId is null) return Results.Unauthorized();
        var user = await db.Users.FindAsync(userId.Value);
        if (user is null) return Results.Unauthorized();
        if (!user.EmailVerified) return Results.Json(new { error = "unverified" }, statusCode: 403);
        return null;
    }
}

public record LoginRequest(string Email);
public record ResendRequest(string Email);
