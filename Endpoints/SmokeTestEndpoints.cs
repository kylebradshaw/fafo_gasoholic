public static class SmokeTestEndpoints
{
    const string UserIdKey = "userId";

    public static void MapSmokeTestEndpoints(this WebApplication app)
    {
        var secret = app.Configuration["SmokeTestSecret"]
                     ?? Environment.GetEnvironmentVariable("SMOKE_TEST_SECRET");

        if (string.IsNullOrEmpty(secret)) return;

        app.MapPost("/auth/test-email", async (
            HttpContext ctx,
            IVerificationEmailSender emailSender) =>
        {
            var provided = ctx.Request.Headers["X-Smoke-Test-Secret"].ToString();
            if (provided != secret)
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var body = await ctx.Request.ReadFromJsonAsync<TestEmailRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email required" });

            if (!emailSender.IsConfigured)
                return Results.Json(new { status = "skipped", reason = "ACS not configured" }, statusCode: 200);

            try
            {
                var messageId = await emailSender.SendTestEmailAsync(body.Email.Trim().ToLowerInvariant());
                return Results.Ok(new { status = "sent", messageId });
            }
            catch (Exception ex)
            {
                return Results.Json(new { status = "error", error = ex.Message }, statusCode: 500);
            }
        });

        app.MapDelete("/auth/dev-cleanup", async (
            HttpContext ctx,
            AppDbContext db) =>
        {
            var provided = ctx.Request.Headers["X-Smoke-Test-Secret"].ToString();
            if (provided != secret)
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var body = await ctx.Request.ReadFromJsonAsync<DevCleanupRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email required" });

            var email = body.Email.Trim().ToLowerInvariant();
            if (!email.EndsWith("@example.com") && !email.EndsWith("@test.com"))
                return Results.BadRequest(new { error = "only @example.com and @test.com addresses may be cleaned up" });

            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Users, u => u.Email == email);
            if (user is null)
                return Results.NotFound(new { error = "user not found" });

            var autoIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Autos.Where(a => a.UserId == user.Id).Select(a => a.Id));
            var fillups = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Fillups.Where(f => autoIds.Contains(f.AutoId)));
            db.Fillups.RemoveRange(fillups);
            var maintenance = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.MaintenanceRecords.Where(m => autoIds.Contains(m.AutoId)));
            db.MaintenanceRecords.RemoveRange(maintenance);
            var autos = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Autos.Where(a => a.UserId == user.Id));
            db.Autos.RemoveRange(autos);
            var tokens = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.VerificationTokens.Where(t => t.UserId == user.Id));
            db.VerificationTokens.RemoveRange(tokens);

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapPost("/dev/dedupe-entries", async (
            HttpContext ctx,
            AppDbContext db) =>
        {
            var provided = ctx.Request.Headers["X-Smoke-Test-Secret"].ToString();
            if (provided != secret)
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var body = await ctx.Request.ReadFromJsonAsync<DedupeRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email required" });

            var email = body.Email.Trim().ToLowerInvariant();
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Users, u => u.Email == email);
            if (user is null)
                return Results.NotFound(new { error = "user not found" });

            var dryRun = body.DryRun;
            var fillupReport = new List<object>();
            var maintenanceReport = new List<object>();
            var fillupsDeleted = 0;
            var maintenanceDeleted = 0;

            var autoIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Autos.Where(a => a.UserId == user.Id).Select(a => a.Id));

            foreach (var autoId in autoIds)
            {
                var fillups = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(db.Fillups.Where(f => f.AutoId == autoId));
                var fillupGroups = fillups
                    .GroupBy(f => new
                    {
                        f.AutoId,
                        FilledAtTicks = f.FilledAt.Ticks,
                        f.Odometer,
                        f.Gallons,
                        f.PricePerGallon,
                        f.FuelType,
                        f.IsPartialFill
                    })
                    .Where(g => g.Count() > 1);
                foreach (var g in fillupGroups)
                {
                    var ordered = g.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
                    var keep = ordered[0];
                    foreach (var dupe in ordered.Skip(1))
                    {
                        fillupReport.Add(new
                        {
                            autoId,
                            keepId = keep.Id,
                            deleteId = dupe.Id,
                            filledAt = keep.FilledAt,
                            odometer = keep.Odometer,
                            gallons = keep.Gallons
                        });
                        if (!dryRun) db.Fillups.Remove(dupe);
                        fillupsDeleted++;
                    }
                }

                var maints = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(db.MaintenanceRecords.Where(m => m.AutoId == autoId));
                var maintGroups = maints
                    .GroupBy(m => new
                    {
                        m.AutoId,
                        PerformedAtTicks = m.PerformedAt.Ticks,
                        m.Type,
                        m.Odometer,
                        m.Cost,
                        m.Notes
                    })
                    .Where(g => g.Count() > 1);
                foreach (var g in maintGroups)
                {
                    var ordered = g.OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
                    var keep = ordered[0];
                    foreach (var dupe in ordered.Skip(1))
                    {
                        maintenanceReport.Add(new
                        {
                            autoId,
                            keepId = keep.Id,
                            deleteId = dupe.Id,
                            performedAt = keep.PerformedAt,
                            type = keep.Type.ToString()
                        });
                        if (!dryRun) db.MaintenanceRecords.Remove(dupe);
                        maintenanceDeleted++;
                    }
                }
            }

            if (!dryRun && (fillupsDeleted > 0 || maintenanceDeleted > 0))
                await db.SaveChangesAsync();

            return Results.Ok(new
            {
                dryRun,
                email,
                fillupsDeleted,
                maintenanceDeleted,
                fillups = fillupReport,
                maintenance = maintenanceReport
            });
        });

        app.MapPost("/auth/dev-login", async (
            HttpContext ctx,
            AppDbContext db) =>
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
                user = new User { Email = email, EmailVerified = true, CreatedAt = DateTime.UtcNow, LastSignIn = DateTime.UtcNow };
                db.Users.Add(user);
                try { await db.SaveChangesAsync(); }
                catch
                {
                    db.Users.Remove(user);
                    user = new User { Email = email, EmailVerified = true, CreatedAt = DateTime.UtcNow };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                user.EmailVerified = true;
                user.LastSignIn = DateTime.UtcNow;
                try { await db.SaveChangesAsync(); }
                catch
                {
                    db.Entry(user).Property(u => u.LastSignIn).IsModified = false;
                    await db.SaveChangesAsync();
                }
            }

            ctx.Session.SetString(UserIdKey, user.Id);
            return Results.Ok(new { status = "ok", email = user.Email });
        });
    }
}

public record DevLoginRequest(string Email);
public record DevCleanupRequest(string Email);
public record TestEmailRequest(string Email);
public record DedupeRequest(string Email, bool DryRun = true);
