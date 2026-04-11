using Microsoft.EntityFrameworkCore;

public static class FillupEndpoints
{
    public static void MapFillupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/autos/{autoId}/fillups", async (string autoId, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == autoId && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            var fillups = await db.Fillups
                .Where(f => f.AutoId == autoId)
                .OrderBy(f => f.Odometer)
                .ToListAsync();

            var rows = ComputeMpg(fillups)
                .OrderByDescending(r => r.FilledAt)
                .ToList();

            return Results.Ok(rows);
        });

        app.MapPost("/api/autos/{autoId}/fillups", async (string autoId, FillupRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == autoId && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            var fillup = new Fillup
            {
                AutoId = autoId,
                FilledAt = DateTime.SpecifyKind(req.FilledAt, DateTimeKind.Utc),
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

        app.MapPut("/api/autos/{autoId}/fillups/{id}", async (string autoId, string id, FillupRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == autoId && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            var fillup = await db.Fillups.FirstOrDefaultAsync(f => f.Id == id && f.AutoId == autoId);
            if (fillup is null) return Results.NotFound();

            fillup.FilledAt = DateTime.SpecifyKind(req.FilledAt, DateTimeKind.Utc);
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

        app.MapDelete("/api/autos/{autoId}/fillups/{id}", async (string autoId, string id, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FirstOrDefaultAsync(a => a.Id == autoId && a.UserId == userId);
            if (auto is null) return Results.NotFound();

            var fillup = await db.Fillups.FirstOrDefaultAsync(f => f.Id == id && f.AutoId == autoId);
            if (fillup is null) return Results.NotFound();

            db.Fillups.Remove(fillup);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    static IEnumerable<FillupRow> ComputeMpg(List<Fillup> fillups)
    {
        for (int i = 0; i < fillups.Count; i++)
        {
            var current = fillups[i];
            double? mpg = null;

            int priorIdx = -1;
            for (int j = i - 1; j >= 0; j--)
            {
                if (!fillups[j].IsPartialFill)
                {
                    priorIdx = j;
                    break;
                }
            }

            if (priorIdx >= 0 && !current.IsPartialFill)
            {
                var prior = fillups[priorIdx];
                var ododelta = (double)(current.Odometer - prior.Odometer);
                var gallons = fillups.Skip(priorIdx + 1).Take(i - priorIdx)
                    .Sum(f => (double)f.Gallons);
                if (gallons > 0 && ododelta > 0)
                    mpg = ododelta / gallons;
            }

            yield return new FillupRow(
                current.Id,
                DateTime.SpecifyKind(current.FilledAt, DateTimeKind.Utc),
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
    string Id,
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
