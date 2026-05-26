using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    const string UserIdKey = "userId";
    const int CodeAttemptLimit = 5;
    static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(30);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest req, AppDbContext db, IVerificationEmailSender emailSender, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required" });

            var email = req.Email.Trim().ToLowerInvariant();

            // If the caller already has an active session for this email, no code needed.
            var sessionUserId = ctx.Session.GetString(UserIdKey);
            if (sessionUserId is not null)
            {
                var sessionUser = await db.Users.FindAsync(sessionUserId);
                if (sessionUser is not null && sessionUser.EmailVerified && sessionUser.Email == email)
                    return Results.Ok(new { status = "ok", email = sessionUser.Email });
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user is null)
            {
                user = new User { Email = email, EmailVerified = false, CreatedAt = DateTime.UtcNow };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            // Verified users re-authenticating get "pending_reauth"; new users get "pending_verification".
            var pendingStatus = user.EmailVerified ? "pending_reauth" : "pending_verification";

            // If an active (unused, non-expired) token already exists, don't send another email.
            // Use Take(1)+ToListAsync rather than AnyAsync to sidestep an EF Cosmos 10.0.5
            // translation bug ("Identifier 'root' could not be resolved") on this predicate shape.
            var userIdForToken = user.Id;
            var now = DateTime.UtcNow;
            var hasActiveToken = (await db.VerificationTokens
                .Where(t => t.UserId == userIdForToken && t.UsedAt == null && t.ExpiresAt > now)
                .Take(1)
                .ToListAsync()).Count > 0;

            if (hasActiveToken)
                return Results.Accepted(null, new { status = pendingStatus });

            var code = GenerateLoginCode();
            db.VerificationTokens.Add(new VerificationToken
            {
                UserId = user.Id,
                Token = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(CodeLifetime)
            });
            await db.SaveChangesAsync();

            await emailSender.SendLoginCodeAsync(user.Email, code);

            return Results.Accepted(null, new { status = pendingStatus });
        });

        app.MapPost("/auth/verify", async (VerifyCodeRequest req, AppDbContext db, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
                return Results.BadRequest(new { error = "email_and_code_required" });

            var email = req.Email.Trim().ToLowerInvariant();
            var submitted = req.Code.Trim();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
                return Results.BadRequest(new { error = "invalid_code" });

            var now = DateTime.UtcNow;
            var userIdForToken = user.Id;
            var active = (await db.VerificationTokens
                .Where(t => t.UserId == userIdForToken && t.UsedAt == null && t.ExpiresAt > now)
                .Take(1)
                .ToListAsync())
                .FirstOrDefault();

            if (active is null)
                return Results.BadRequest(new { error = "code_expired" });

            if (active.Attempts >= CodeAttemptLimit)
            {
                active.UsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.BadRequest(new { error = "too_many_attempts" });
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(submitted),
                    System.Text.Encoding.UTF8.GetBytes(active.Token)))
            {
                active.Attempts++;
                if (active.Attempts >= CodeAttemptLimit)
                    active.UsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.BadRequest(new
                {
                    error = active.UsedAt is not null ? "too_many_attempts" : "invalid_code",
                    attemptsRemaining = Math.Max(0, CodeAttemptLimit - active.Attempts)
                });
            }

            active.UsedAt = DateTime.UtcNow;
            user.EmailVerified = true;
            user.LastSignIn = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync();
            }
            catch
            {
                db.Entry(user).Property(u => u.LastSignIn).IsModified = false;
                await db.SaveChangesAsync();
            }

            ctx.Session.SetString(UserIdKey, user.Id);
            return Results.Ok(new { status = "ok", email = user.Email });
        });

        app.MapPost("/auth/resend", async (ResendRequest req, AppDbContext db, IVerificationEmailSender emailSender, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required" });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
                return Results.Ok(new { status = "ok" });  // Don't reveal whether user exists

            // Verified users may resend only if they're mid re-auth (have an active unused token).
            // Without that guard, anyone could trigger emails to any verified address.
            if (user.EmailVerified)
            {
                var now = DateTime.UtcNow;
                var userIdForReauth = user.Id;
                var hasActiveTokenForReauth = (await db.VerificationTokens
                    .Where(t => t.UserId == userIdForReauth && t.UsedAt == null && t.ExpiresAt > now)
                    .Take(1)
                    .ToListAsync()).Count > 0;
                if (!hasActiveTokenForReauth)
                    return Results.Ok(new { status = "ok" });
            }

            var cutoff = DateTime.UtcNow.AddHours(-1);
            var recentCount = await db.VerificationTokens
                .CountAsync(t => t.UserId == user.Id && t.CreatedAt >= cutoff);

            if (recentCount >= 4)  // login + 3 resends = 4 total tokens before lockout
                return Results.Json(new { error = "too_many_requests" }, statusCode: 429);

            // Invalidate old unused tokens (mark as used so they're still counted in rate limit)
            var unusedTokens = await db.VerificationTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null)
                .ToListAsync();
            foreach (var t in unusedTokens)
                t.UsedAt = DateTime.UtcNow;

            var code = GenerateLoginCode();
            db.VerificationTokens.Add(new VerificationToken
            {
                UserId = user.Id,
                Token = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(CodeLifetime)
            });
            await db.SaveChangesAsync();

            await emailSender.SendLoginCodeAsync(user.Email, code);

            return Results.Accepted(null, new { status = "pending" });
        });

        app.MapPost("/auth/logout", (HttpContext ctx) =>
        {
            ctx.Session.Clear();
            return Results.Ok();
        });

        app.MapGet("/auth/me", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.Session.GetString(UserIdKey);
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.Unauthorized();
            if (!user.EmailVerified) return Results.Json(new { error = "unverified" }, statusCode: 403);

            if (ctx.Session.GetInt32("interactionLogged") is null)
            {
                try
                {
                    user.LastInteraction = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    ctx.Session.SetInt32("interactionLogged", 1);
                }
                catch
                {
                    db.Entry(user).Property(u => u.LastInteraction).IsModified = false;
                }
            }

            return Results.Ok(new { email = user.Email });
        });
    }

    static string GenerateLoginCode()
    {
        // 6 random digits, uniformly distributed. Leading zeros preserved.
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D6");
    }

    public static string? GetUserId(this HttpContext ctx) =>
        ctx.Session.GetString(UserIdKey);

    public static async Task<IResult?> RequireAuth(HttpContext ctx, AppDbContext db)
    {
        var userId = ctx.Session.GetString(UserIdKey);
        if (userId is null) return Results.Unauthorized();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Results.Unauthorized();
        if (!user.EmailVerified) return Results.Json(new { error = "unverified" }, statusCode: 403);
        return null;
    }
}

public record LoginRequest(string Email);
public record ResendRequest(string Email);
public record VerifyCodeRequest(string Email, string Code);
