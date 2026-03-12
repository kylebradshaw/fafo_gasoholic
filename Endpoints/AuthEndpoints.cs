using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    const string UserIdKey = "userId";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest req, AppDbContext db, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required" });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new User { Email = email };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            ctx.Session.SetInt32(UserIdKey, user.Id);
            return Results.Ok(new { email = user.Email });
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
        return user is null ? Results.Unauthorized() : null;
    }
}

public record LoginRequest(string Email);
