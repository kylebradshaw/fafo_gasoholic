using Microsoft.EntityFrameworkCore;

public static class FillupEndpoints
{
    public static void MapFillupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/autos/{autoId:int}/fillups", async (int autoId, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var fillups = await db.Fillups
                .Where(f => f.AutoId == autoId)
                .OrderBy(f => f.Odometer)
                .ToListAsync();

            var rows = ComputeMpg(fillups)
                .OrderByDescending(r => r.FilledAt)
                .ToList();

            return Results.Ok(rows);
        });

        app.MapPost("/api/autos/{autoId:int}/fillups", async (int autoId, FillupRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var fillup = new Fillup
            {
                AutoId = autoId,
                FilledAt = req.FilledAt,
                Location = req.Location,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                FuelType = req.FuelType,
                PricePerGallon = req.PricePerGallon,
                Gallons = req.Gallons,
                Odometer = req.Odometer,
                IsPartialFill = req.IsPartialFill
            };
            db.Fillups.Add(fillup);
            await db.SaveChangesAsync();

            return Results.Created($"/api/autos/{autoId}/fillups/{fillup.Id}",
                new { fillup.Id });
        });

        app.MapPut("/api/autos/{autoId:int}/fillups/{id:int}", async (int autoId, int id, FillupRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var fillup = await db.Fillups.FirstOrDefaultAsync(f => f.Id == id && f.AutoId == autoId);
            if (fillup is null) return Results.NotFound();

            fillup.FilledAt = req.FilledAt;
            fillup.Location = req.Location;
            fillup.Latitude = req.Latitude;
            fillup.Longitude = req.Longitude;
            fillup.FuelType = req.FuelType;
            fillup.PricePerGallon = req.PricePerGallon;
            fillup.Gallons = req.Gallons;
            fillup.Odometer = req.Odometer;
            fillup.IsPartialFill = req.IsPartialFill;
            await db.SaveChangesAsync();

            return Results.Ok(new { fillup.Id });
        });

        app.MapDelete("/api/autos/{autoId:int}/fillups/{id:int}", async (int autoId, int id, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!.Value;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var fillup = await db.Fillups.FirstOrDefaultAsync(f => f.Id == id && f.AutoId == autoId);
            if (fillup is null) return Results.NotFound();

            db.Fillups.Remove(fillup);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    // MPG logic:
    // Sort by odometer ascending. For each full fill, find the nearest prior full fill.
    // Sum all gallons between them (inclusive of partials in between) and divide into the odometer delta.
    static IEnumerable<FillupRow> ComputeMpg(List<Fillup> fillups)
    {
        // fillups already sorted by odometer ascending
        for (int i = 0; i < fillups.Count; i++)
        {
            var current = fillups[i];
            double? mpg = null;

            if (!current.IsPartialFill)
            {
                // Find the most recent prior full fill
                int priorIdx = -1;
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!fillups[j].IsPartialFill)
                    {
                        priorIdx = j;
                        break;
                    }
                }

                if (priorIdx >= 0)
                {
                    var prior = fillups[priorIdx];
                    var ododelta = (double)(current.Odometer - prior.Odometer);
                    // Sum gallons from priorIdx+1 through i (all fills between prior full fill and this one)
                    var gallons = fillups.Skip(priorIdx + 1).Take(i - priorIdx)
                        .Sum(f => (double)f.Gallons);
                    if (gallons > 0 && ododelta > 0)
                        mpg = ododelta / gallons;
                }
            }

            yield return new FillupRow(
                current.Id,
                current.FilledAt,
                current.Location,
                current.Latitude,
                current.Longitude,
                current.FuelType.ToString(),
                current.PricePerGallon,
                current.Gallons,
                current.Odometer,
                current.IsPartialFill,
                mpg.HasValue ? Math.Round(mpg.Value, 1) : null
            );
        }
    }
}

public record FillupRequest(
    DateTime FilledAt,
    string? Location,
    double? Latitude,
    double? Longitude,
    FuelType FuelType,
    decimal PricePerGallon,
    decimal Gallons,
    decimal Odometer,
    bool IsPartialFill
);

public record FillupRow(
    int Id,
    DateTime FilledAt,
    string? Location,
    double? Latitude,
    double? Longitude,
    string FuelType,
    decimal PricePerGallon,
    decimal Gallons,
    decimal Odometer,
    bool IsPartialFill,
    double? Mpg
);
