using Microsoft.EntityFrameworkCore;

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/autos/{autoId}/maintenance", async (string autoId, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var records = await db.MaintenanceRecords
                .Where(m => m.AutoId == autoId)
                .OrderByDescending(m => m.Odometer)
                .Select(m => new MaintenanceRow(
                    m.Id,
                    m.Type.ToString(),
                    DateTime.SpecifyKind(m.PerformedAt, DateTimeKind.Utc),
                    m.Odometer,
                    m.Cost,
                    m.Notes
                ))
                .ToListAsync();

            return Results.Ok(records);
        });

        app.MapPost("/api/autos/{autoId}/maintenance", async (string autoId, MaintenanceRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var record = new MaintenanceRecord
            {
                AutoId = autoId,
                Type = req.Type,
                PerformedAt = DateTime.SpecifyKind(req.PerformedAt, DateTimeKind.Utc),
                Odometer = req.Odometer,
                Cost = req.Cost,
                Notes = req.Notes
            };
            db.MaintenanceRecords.Add(record);
            await db.SaveChangesAsync();

            return Results.Created($"/api/autos/{autoId}/maintenance/{record.Id}", new { record.Id });
        });

        app.MapPut("/api/autos/{autoId}/maintenance/{id}", async (string autoId, string id, MaintenanceRequest req, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var record = await db.MaintenanceRecords.FirstOrDefaultAsync(m => m.Id == id && m.AutoId == autoId);
            if (record is null) return Results.NotFound();

            record.Type = req.Type;
            record.PerformedAt = DateTime.SpecifyKind(req.PerformedAt, DateTimeKind.Utc);
            record.Odometer = req.Odometer;
            record.Cost = req.Cost;
            record.Notes = req.Notes;
            await db.SaveChangesAsync();

            return Results.Ok(new { record.Id });
        });

        app.MapDelete("/api/autos/{autoId}/maintenance/{id}", async (string autoId, string id, HttpContext ctx, AppDbContext db) =>
        {
            var auth = await AuthEndpoints.RequireAuth(ctx, db);
            if (auth is not null) return auth;

            var userId = ctx.GetUserId()!;
            var auto = await db.Autos.FindAsync(autoId);
            if (auto is null) return Results.NotFound();
            if (auto.UserId != userId) return Results.StatusCode(403);

            var record = await db.MaintenanceRecords.FirstOrDefaultAsync(m => m.Id == id && m.AutoId == autoId);
            if (record is null) return Results.NotFound();

            db.MaintenanceRecords.Remove(record);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}

public record MaintenanceRequest(
    MaintenanceType Type,
    DateTime PerformedAt,
    decimal Odometer,
    decimal Cost,
    string? Notes
);

public record MaintenanceRow(
    string Id,
    string Type,
    DateTime PerformedAt,
    decimal Odometer,
    decimal Cost,
    string? Notes
);
