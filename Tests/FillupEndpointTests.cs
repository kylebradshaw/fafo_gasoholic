using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Integration tests for the fillup endpoints:
///   GET    /api/autos/{autoId}/fillups
///   POST   /api/autos/{autoId}/fillups
///   PUT    /api/autos/{autoId}/fillups/{id}
///   DELETE /api/autos/{autoId}/fillups/{id}
/// </summary>
public class FillupEndpointTests(GasoholicWebAppFactory factory) : IntegrationTestBase(factory)
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<(HttpClient client, int autoId)> CreateAutoAsync(string? emailSuffix = null)
    {
        var email = $"fillup-{emailSuffix ?? Guid.NewGuid().ToString()}@test.com";
        var client = await CreateAuthenticatedClientAsync(email);
        var resp = await client.PostAsJsonAsync("/api/autos",
            new { brand = "Honda", model = "Civic", plate = "HND1", odometer = 0m });
        resp.EnsureSuccessStatusCode();
        var autoId = (await ReadJsonAsync(resp)).GetProperty("id").GetInt32();
        return (client, autoId);
    }

    private static object MakeFillupRequest(
        decimal odometer = 10000m, decimal gallons = 10m, bool isPartial = false,
        int fuelType = 0 /* FuelType.Regular */, decimal pricePerGallon = 3.50m,
        DateTime? filledAt = null) => new
        {
            filledAt = filledAt ?? DateTime.UtcNow,
            location = (string?)null,
            latitude = (double?)null,
            longitude = (double?)null,
            fuelType,
            pricePerGallon,
            gallons,
            odometer,
            isPartialFill = isPartial
        };

    // ── POST /api/autos/{autoId}/fillups ───────────────────────────────────────

    [Fact]
    public async Task PostFillup_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/api/autos/1/fillups", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostFillup_NonExistentAuto_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"post404auto-{Guid.NewGuid()}@test.com");

        var resp = await client.PostAsJsonAsync("/api/autos/99999/fillups", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PostFillup_OtherUsersAuto_Returns403()
    {
        var (ownerClient, autoId) = await CreateAutoAsync();
        var otherClient = await CreateAuthenticatedClientAsync($"postother-{Guid.NewGuid()}@test.com");

        var resp = await otherClient.PostAsJsonAsync($"/api/autos/{autoId}/fillups", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PostFillup_ValidData_Returns201WithId()
    {
        var (client, autoId) = await CreateAutoAsync();

        var resp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 15000m, gallons: 12m));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.True(doc.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task PostFillup_AppearsInGetList()
    {
        var (client, autoId) = await CreateAutoAsync();

        await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 20000m, gallons: 9m));

        var resp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(resp)).EnumerateArray().ToList();

        Assert.Single(rows);
        Assert.Equal(20000m, rows[0].GetProperty("odometer").GetDecimal());
        Assert.Equal(9m, rows[0].GetProperty("gallons").GetDecimal());
    }

    [Fact]
    public async Task PostFillup_AllFuelTypes_AcceptedSuccessfully()
    {
        var (client, autoId) = await CreateAutoAsync();
        // FuelType enum: Regular=0, MidGrade=1, Premium=2, Diesel=3, E85=4
        var fuelTypes = new[] { 0, 1, 2, 3, 4 };

        for (int i = 0; i < fuelTypes.Length; i++)
        {
            var resp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
                MakeFillupRequest(odometer: 10000m + i * 100, gallons: 10m, fuelType: fuelTypes[i]));

            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }
    }

    [Fact]
    public async Task PostFillup_WithLocation_PersistsLocationData()
    {
        var (client, autoId) = await CreateAutoAsync();

        var req = new
        {
            filledAt = DateTime.UtcNow,
            location = "Shell Station, Main St",
            latitude = 37.7749,
            longitude = -122.4194,
            fuelType = 2,       // FuelType.Premium = 2
            pricePerGallon = 4.29m,
            gallons = 11.5m,
            odometer = 50000m,
            isPartialFill = false
        };

        await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups", req);

        var resp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(resp)).EnumerateArray().ToList();

        Assert.Single(rows);
        Assert.Equal("Shell Station, Main St", rows[0].GetProperty("location").GetString());
        Assert.Equal(37.7749, rows[0].GetProperty("latitude").GetDouble(), precision: 4);
        Assert.Equal(-122.4194, rows[0].GetProperty("longitude").GetDouble(), precision: 4);
    }

    // ── PUT /api/autos/{autoId}/fillups/{id} ───────────────────────────────────

    [Fact]
    public async Task PutFillup_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.PutAsJsonAsync("/api/autos/1/fillups/1", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PutFillup_NonExistentAuto_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"put404auto-{Guid.NewGuid()}@test.com");

        var resp = await client.PutAsJsonAsync("/api/autos/99999/fillups/1", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PutFillup_OtherUsersAuto_Returns403()
    {
        var (ownerClient, autoId) = await CreateAutoAsync();
        var otherClient = await CreateAuthenticatedClientAsync($"putother-{Guid.NewGuid()}@test.com");

        var resp = await otherClient.PutAsJsonAsync($"/api/autos/{autoId}/fillups/1", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PutFillup_NonExistentFillup_Returns404()
    {
        var (client, autoId) = await CreateAutoAsync();

        var resp = await client.PutAsJsonAsync($"/api/autos/{autoId}/fillups/99999", MakeFillupRequest());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PutFillup_ValidUpdate_ReturnsUpdatedData()
    {
        var (client, autoId) = await CreateAutoAsync();

        var createResp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 30000m, gallons: 10m));
        var fillupId = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var updateResp = await client.PutAsJsonAsync($"/api/autos/{autoId}/fillups/{fillupId}",
            MakeFillupRequest(odometer: 30000m, gallons: 12.5m, pricePerGallon: 4.00m));

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Verify update is reflected in GET
        var listResp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(listResp)).EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal(12.5m, rows[0].GetProperty("gallons").GetDecimal());
        Assert.Equal(4.00m, rows[0].GetProperty("pricePerGallon").GetDecimal());
    }

    [Fact]
    public async Task PutFillup_CanTogglePartialFlag()
    {
        var (client, autoId) = await CreateAutoAsync();

        var createResp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 10000m, gallons: 10m, isPartial: false));
        var fillupId = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        // Change to partial
        await client.PutAsJsonAsync($"/api/autos/{autoId}/fillups/{fillupId}",
            MakeFillupRequest(odometer: 10000m, gallons: 5m, isPartial: true));

        var listResp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(listResp)).EnumerateArray().ToList();
        Assert.True(rows[0].GetProperty("isPartialFill").GetBoolean());
    }

    // ── DELETE /api/autos/{autoId}/fillups/{id} ────────────────────────────────

    [Fact]
    public async Task DeleteFillup_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.DeleteAsync("/api/autos/1/fillups/1");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFillup_NonExistentAuto_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"del404auto-{Guid.NewGuid()}@test.com");

        var resp = await client.DeleteAsync("/api/autos/99999/fillups/1");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFillup_OtherUsersAuto_Returns403()
    {
        var (ownerClient, autoId) = await CreateAutoAsync();
        var otherClient = await CreateAuthenticatedClientAsync($"delother-{Guid.NewGuid()}@test.com");

        var resp = await otherClient.DeleteAsync($"/api/autos/{autoId}/fillups/1");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFillup_NonExistentFillup_Returns404()
    {
        var (client, autoId) = await CreateAutoAsync();

        var resp = await client.DeleteAsync($"/api/autos/{autoId}/fillups/99999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFillup_ValidId_Returns204AndRemovedFromList()
    {
        var (client, autoId) = await CreateAutoAsync();

        var createResp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 40000m, gallons: 10m));
        var fillupId = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var deleteResp = await client.DeleteAsync($"/api/autos/{autoId}/fillups/{fillupId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listResp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(listResp)).EnumerateArray().ToList();
        Assert.DoesNotContain(rows, r => r.GetProperty("id").GetInt32() == fillupId);
    }

    [Fact]
    public async Task DeleteFillup_DeleteOneOfMultiple_OthersRemain()
    {
        var (client, autoId) = await CreateAutoAsync();

        var r1 = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 10000m, gallons: 10m));
        var r2 = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillupRequest(odometer: 10300m, gallons: 10m));

        var id1 = (await ReadJsonAsync(r1)).GetProperty("id").GetInt32();

        await client.DeleteAsync($"/api/autos/{autoId}/fillups/{id1}");

        var listResp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        var rows = (await ReadJsonAsync(listResp)).EnumerateArray().ToList();

        Assert.Single(rows);
        Assert.NotEqual(id1, rows[0].GetProperty("id").GetInt32());
    }
}
