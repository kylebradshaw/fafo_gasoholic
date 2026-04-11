using Microsoft.EntityFrameworkCore;

public static class AutoEndpoints
{
    public static void MapAutoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/autos", async (HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var autos = await db.Autos
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Brand).ThenBy(a => a.Model)
                .ToListAsync();

            var result = new List<object>(autos.Count);
            foreach (var a in autos)
            {
                var latest = await db.Fillups
                    .Where(f => f.AutoId == a.Id)
                    .OrderByDescending(f => f.FilledAt)
                    .Select(f => new { f.FilledAt, f.Odometer })
                    .FirstOrDefaultAsync();
                result.Add(new
                {
                    a.Id, a.Brand, a.Model, a.Plate, a.Odometer,
                    LatestFillupAt = (DateTime?)latest?.FilledAt,
                    LatestFillupOdometer = (decimal?)latest?.Odometer
                });
            }

            return Results.Ok(result);
        });

        app.MapPost("/api/autos", async (AutoRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = new Auto
            {
                UserId = userId,
                Brand = req.Brand,
                Model = req.Model,
                Plate = req.Plate,
                Odometer = req.Odometer
            };
            db.Autos.Add(auto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/autos/{auto.Id}",
                new { auto.Id, auto.Brand, auto.Model, auto.Plate, auto.Odometer });
        });

        app.MapPut("/api/autos/{id}", async (string id, AutoRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            auto.Brand = req.Brand;
            auto.Model = req.Model;
            auto.Plate = req.Plate;
            auto.Odometer = req.Odometer;
            await db.SaveChangesAsync();

            return Results.Ok(new { auto.Id, auto.Brand, auto.Model, auto.Plate, auto.Odometer });
        });

        app.MapDelete("/api/autos/{id}", async (string id, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            var fillups = await db.Fillups.Where(f => f.AutoId == id).ToListAsync();
            db.Fillups.RemoveRange(fillups);

            var maintenance = await db.MaintenanceRecords.Where(m => m.AutoId == id).ToListAsync();
            db.MaintenanceRecords.RemoveRange(maintenance);

            db.Autos.Remove(auto);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record AutoRequest(string Brand, string Model, string Plate, decimal Odometer);
