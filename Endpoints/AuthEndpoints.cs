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

            // If the caller already has an active session for this email, no magic link needed.
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
            var now = DateTime.UtcNow;
            var hasActiveToken = await db.VerificationTokens
                .AnyAsync(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now);

            if (hasActiveToken)
                return Results.Accepted(null, new { status = pendingStatus });

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

            var baseUrl = GetBaseUrl(ctx);
            await emailSender.SendMagicLinkAsync(user.Email, token, baseUrl);

            return Results.Accepted(null, new { status = pendingStatus });
        });

        app.MapGet("/auth/verify", async (string token, AppDbContext db, HttpContext ctx) =>
        {
            var vt = await db.VerificationTokens.FirstOrDefaultAsync(t => t.Token == token);

            if (vt is null)
                return Results.BadRequest(new { error = "token_not_found" });
            if (vt.UsedAt is not null)
                return Results.BadRequest(new { error = "token_used" });
            if (vt.ExpiresAt <= DateTime.UtcNow)
                return Results.BadRequest(new { error = "token_expired" });

            var user = await db.Users.FindAsync(vt.UserId);
            if (user is null)
                return Results.BadRequest(new { error = "user_not_found" });

            vt.UsedAt = DateTime.UtcNow;
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
            return Results.Redirect("/app");
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
                var hasActiveTokenForReauth = await db.VerificationTokens
                    .AnyAsync(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now);
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

            var token = Guid.NewGuid().ToString("N");
            db.VerificationTokens.Add(new VerificationToken
            {
                UserId = user.Id,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();

            var baseUrl = GetBaseUrl(ctx);
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

    private static string GetBaseUrl(HttpContext ctx)
    {
        var origin = ctx.Request.Headers.Origin.FirstOrDefault()
                  ?? ctx.Request.Headers.Referer.FirstOrDefault();
        if (origin is not null)
        {
            var uri = new Uri(origin);
            return $"{uri.Scheme}://{uri.Authority}";
        }
        return $"{ctx.Request.Scheme}://{ctx.Request.Host}";
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
