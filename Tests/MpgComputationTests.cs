using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Tests for the MPG computation algorithm in FillupEndpoints.ComputeMpg().
/// Tests exercise the algorithm through the GET /api/autos/{autoId}/fillups endpoint.
/// </summary>
public class MpgComputationTests(GasoholicWebAppFactory factory) : IntegrationTestBase(factory)
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<(HttpClient client, int autoId)> SetupAutoAsync()
    {
        var client = await CreateAuthenticatedClientAsync($"mpg-{Guid.NewGuid()}@test.com");
        var resp = await client.PostAsJsonAsync("/api/autos",
            new { brand = "Toyota", model = "Camry", plate = "TEST1", odometer = 0m });
        resp.EnsureSuccessStatusCode();
        var doc = await ReadJsonAsync(resp);
        int autoId = doc.GetProperty("id").GetInt32();
        return (client, autoId);
    }

    private static object MakeFillup(decimal odometer, decimal gallons, bool isPartial = false) => new
    {
        filledAt = DateTime.UtcNow,
        location = (string?)null,
        latitude = (double?)null,
        longitude = (double?)null,
        fuelType = 0,           // FuelType.Regular = 0
        pricePerGallon = 3.50m,
        gallons,
        odometer,
        isPartialFill = isPartial
    };

    private async Task AddFillupAsync(HttpClient client, int autoId, decimal odometer,
        decimal gallons, bool isPartial = false)
    {
        var resp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillup(odometer, gallons, isPartial));
        resp.EnsureSuccessStatusCode();
    }

    private async Task<List<JsonElement>> GetFillupsAsync(HttpClient client, int autoId)
    {
        var resp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        resp.EnsureSuccessStatusCode();
        var doc = await ReadJsonAsync(resp);
        return doc.EnumerateArray().ToList();
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoFillups_ReturnsEmptyList()
    {
        var (client, autoId) = await SetupAutoAsync();

        var rows = await GetFillupsAsync(client, autoId);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task SingleFullFill_HasNoMpg()
    {
        var (client, autoId) = await SetupAutoAsync();
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);

        var rows = await GetFillupsAsync(client, autoId);

        Assert.Single(rows);
        Assert.True(rows[0].GetProperty("mpg").ValueKind == JsonValueKind.Null,
            "First full fill with no prior full fill should have null MPG");
    }

    [Fact]
    public async Task TwoFullFills_SecondHasMpg()
    {
        var (client, autoId) = await SetupAutoAsync();
        // Fill 1: start baseline at 10 000 mi, 10 gal
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        // Fill 2: 10 300 mi later, 10 gal → expected MPG = 300 / 10 = 30.0
        await AddFillupAsync(client, autoId, odometer: 10300, gallons: 10);

        var rows = await GetFillupsAsync(client, autoId);

        // Results ordered newest-first by FilledAt; with same-second timestamps ordering may vary
        var fullFillRows = rows.Where(r => !r.GetProperty("isPartialFill").GetBoolean()).ToList();
        var withMpg = fullFillRows.Where(r => r.GetProperty("mpg").ValueKind != JsonValueKind.Null).ToList();

        Assert.Single(withMpg);
        Assert.Equal(30.0, withMpg[0].GetProperty("mpg").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task PartialFillBetweenFullFills_MpgAccountsForPartialGallons()
    {
        var (client, autoId) = await SetupAutoAsync();
        // Full fill at 10 000, 10 gal
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        // Partial fill at 10 150, 5 gal (not counted in odometer delta, but gallons ARE summed)
        await AddFillupAsync(client, autoId, odometer: 10150, gallons: 5, isPartial: true);
        // Full fill at 10 300, 8 gal
        // Odometer delta: 10 300 - 10 000 = 300
        // Gallons summed (priorIdx+1 through current): 5 (partial) + 8 (this fill) = 13
        // Expected MPG = 300 / 13 ≈ 23.1
        await AddFillupAsync(client, autoId, odometer: 10300, gallons: 8);

        var rows = await GetFillupsAsync(client, autoId);

        var fullFills = rows.Where(r => !r.GetProperty("isPartialFill").GetBoolean()).ToList();
        var secondFull = fullFills.First(r => r.GetProperty("mpg").ValueKind != JsonValueKind.Null);

        var expectedMpg = Math.Round(300.0 / 13.0, 1);
        Assert.Equal(expectedMpg, secondFull.GetProperty("mpg").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task PartialFillAlwaysHasNullMpg()
    {
        var (client, autoId) = await SetupAutoAsync();
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        await AddFillupAsync(client, autoId, odometer: 10100, gallons: 5, isPartial: true);

        var rows = await GetFillupsAsync(client, autoId);

        var partialRows = rows.Where(r => r.GetProperty("isPartialFill").GetBoolean()).ToList();
        Assert.All(partialRows, r =>
            Assert.True(r.GetProperty("mpg").ValueKind == JsonValueKind.Null,
                "Partial fills should never have an MPG value"));
    }

    [Fact]
    public async Task FirstFillupIsPartial_NoMpgForAnyRow()
    {
        var (client, autoId) = await SetupAutoAsync();
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 5, isPartial: true);
        await AddFillupAsync(client, autoId, odometer: 10200, gallons: 10);

        var rows = await GetFillupsAsync(client, autoId);

        // The full fill at 10 200 has no prior full fill reference, so MPG must be null
        var fullFillRow = rows.First(r => !r.GetProperty("isPartialFill").GetBoolean());
        Assert.True(fullFillRow.GetProperty("mpg").ValueKind == JsonValueKind.Null,
            "Full fill whose only prior fill is partial should have null MPG");
    }

    [Fact]
    public async Task AllPartialFills_NoneHaveMpg()
    {
        var (client, autoId) = await SetupAutoAsync();
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 3, isPartial: true);
        await AddFillupAsync(client, autoId, odometer: 10100, gallons: 4, isPartial: true);
        await AddFillupAsync(client, autoId, odometer: 10200, gallons: 2, isPartial: true);

        var rows = await GetFillupsAsync(client, autoId);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r =>
            Assert.True(r.GetProperty("mpg").ValueKind == JsonValueKind.Null));
    }

    [Fact]
    public async Task MpgRoundedToOneDecimalPlace()
    {
        var (client, autoId) = await SetupAutoAsync();
        // 10 000 mi baseline
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        // 301 miles / 7 gallons = 43.0000… → rounded to 43.0
        await AddFillupAsync(client, autoId, odometer: 10301, gallons: 7);

        var rows = await GetFillupsAsync(client, autoId);

        var mpgRow = rows.First(r => r.GetProperty("mpg").ValueKind != JsonValueKind.Null);
        var mpg = mpgRow.GetProperty("mpg").GetDouble();
        var expectedRaw = 301.0 / 7.0;
        Assert.Equal(Math.Round(expectedRaw, 1), mpg, precision: 1);
    }

    [Fact]
    public async Task MultipleConsecutiveFullFills_EachGetIndependentMpg()
    {
        var (client, autoId) = await SetupAutoAsync();
        // Fill 1: baseline at 0, 10 gal
        await AddFillupAsync(client, autoId, odometer: 0, gallons: 10);
        // Fill 2: +200 mi, 10 gal → MPG = 200/10 = 20.0
        await AddFillupAsync(client, autoId, odometer: 200, gallons: 10);
        // Fill 3: +300 mi, 10 gal → MPG = 300/10 = 30.0
        await AddFillupAsync(client, autoId, odometer: 500, gallons: 10);

        var rows = await GetFillupsAsync(client, autoId);

        var withMpg = rows
            .Where(r => r.GetProperty("mpg").ValueKind != JsonValueKind.Null)
            .OrderBy(r => r.GetProperty("odometer").GetDecimal())
            .ToList();

        Assert.Equal(2, withMpg.Count);
        Assert.Equal(20.0, withMpg[0].GetProperty("mpg").GetDouble(), precision: 1);
        Assert.Equal(30.0, withMpg[1].GetProperty("mpg").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task ResultsReturnedNewestFirst()
    {
        var (client, autoId) = await SetupAutoAsync();
        // Add fillups with distinct filledAt times to verify ordering
        var t = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var content = new
            {
                filledAt = t.AddHours(i),
                location = (string?)null,
                latitude = (double?)null,
                longitude = (double?)null,
                fuelType = 0,           // FuelType.Regular = 0
                pricePerGallon = 3.50m,
                gallons = 10m,
                odometer = 10000m + (i * 100),
                isPartialFill = false
            };
            var resp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups", content);
            resp.EnsureSuccessStatusCode();
        }

        var rows = await GetFillupsAsync(client, autoId);

        Assert.Equal(3, rows.Count);
        // Verify filledAt is descending (newest first)
        var dates = rows.Select(r => r.GetProperty("filledAt").GetDateTime()).ToList();
        for (int i = 0; i < dates.Count - 1; i++)
            Assert.True(dates[i] >= dates[i + 1], "Fillups should be ordered newest-first");
    }

    [Fact]
    public async Task GetFillups_UnknownAuto_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"mpg404-{Guid.NewGuid()}@test.com");

        var resp = await client.GetAsync("/api/autos/99999/fillups");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetFillups_OtherUsersAuto_Returns403()
    {
        var (ownerClient, autoId) = await SetupAutoAsync();
        var otherClient = await CreateAuthenticatedClientAsync($"other-{Guid.NewGuid()}@test.com");

        var resp = await otherClient.GetAsync($"/api/autos/{autoId}/fillups");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetFillups_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.GetAsync("/api/autos/1/fillups");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
