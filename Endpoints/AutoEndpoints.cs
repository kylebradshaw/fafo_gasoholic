using Microsoft.EntityFrameworkCore;

public static class AutoEndpoints
{
    public static void MapAutoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/autos", async (HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var autos = await db.Autos
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Brand).ThenBy(a => a.Model)
                .Select(a => new
                {
                    a.Id, a.Brand, a.Model, a.Plate, a.Odometer,
                    LatestFillupAt = db.Fillups
                        .Where(f => f.AutoId == a.Id)
                        .Max(f => (DateTime?)f.FilledAt),
                    LatestFillupOdometer = db.Fillups
                        .Where(f => f.AutoId == a.Id)
                        .Max(f => (decimal?)f.Odometer)
                })
                .ToListAsync();

            return Results.Ok(autos);
        });

        app.MapPost("/api/autos", async (AutoRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
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

        app.MapPut("/api/autos/{id:int}", async (int id, AutoRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(id);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            auto.Brand = req.Brand;
            auto.Model = req.Model;
            auto.Plate = req.Plate;
            auto.Odometer = req.Odometer;
            await db.SaveChangesAsync();

            return Results.Ok(new { auto.Id, auto.Brand, auto.Model, auto.Plate, auto.Odometer });
        });

        app.MapDelete("/api/autos/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(id);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            db.Autos.Remove(auto);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record AutoRequest(string Brand, string Model, string Plate, decimal Odometer);
