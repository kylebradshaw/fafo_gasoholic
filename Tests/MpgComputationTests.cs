using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class MpgComputationTests(GasoholicWebAppFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(HttpClient client, string autoId)> SetupAutoAsync()
    {
        var client = await CreateAuthenticatedClientAsync($"mpg-{Guid.NewGuid()}@test.com");
        var resp = await client.PostAsJsonAsync("/api/autos",
            new { brand = "Toyota", model = "Camry", plate = "TEST1", odometer = 0m });
        resp.EnsureSuccessStatusCode();
        var doc = await ReadJsonAsync(resp);
        string autoId = doc.GetProperty("id").GetString()!;
        return (client, autoId);
    }

    private static object MakeFillup(decimal odometer, decimal gallons, bool isPartial = false) => new
    {
        filledAt = DateTime.UtcNow,
        location = (string?)null,
        latitude = (double?)null,
        longitude = (double?)null,
        fuelType = 0,
        pricePerGallon = 3.50m,
        gallons,
        odometer,
        isPartialFill = isPartial
    };

    private async Task AddFillupAsync(HttpClient client, string autoId, decimal odometer,
        decimal gallons, bool isPartial = false)
    {
        var resp = await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups",
            MakeFillup(odometer, gallons, isPartial));
        resp.EnsureSuccessStatusCode();
    }

    private async Task<List<JsonElement>> GetFillupsAsync(HttpClient client, string autoId)
    {
        var resp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        resp.EnsureSuccessStatusCode();
        var doc = await ReadJsonAsync(resp);
        return doc.EnumerateArray().ToList();
    }

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
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        await AddFillupAsync(client, autoId, odometer: 10300, gallons: 10);

        var rows = await GetFillupsAsync(client, autoId);

        var fullFillRows = rows.Where(r => !r.GetProperty("isPartialFill").GetBoolean()).ToList();
        var withMpg = fullFillRows.Where(r => r.GetProperty("mpg").ValueKind != JsonValueKind.Null).ToList();

        Assert.Single(withMpg);
        Assert.Equal(30.0, withMpg[0].GetProperty("mpg").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task PartialFillBetweenFullFills_MpgAccountsForPartialGallons()
    {
        var (client, autoId) = await SetupAutoAsync();
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
        await AddFillupAsync(client, autoId, odometer: 10150, gallons: 5, isPartial: true);
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
        await AddFillupAsync(client, autoId, odometer: 10000, gallons: 10);
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
        await AddFillupAsync(client, autoId, odometer: 0, gallons: 10);
        await AddFillupAsync(client, autoId, odometer: 200, gallons: 10);
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
        var t = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var content = new
            {
                filledAt = t.AddHours(i),
                location = (string?)null,
                latitude = (double?)null,
                longitude = (double?)null,
                fuelType = 0,
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
        var dates = rows.Select(r => r.GetProperty("filledAt").GetDateTime()).ToList();
        for (int i = 0; i < dates.Count - 1; i++)
            Assert.True(dates[i] >= dates[i + 1], "Fillups should be ordered newest-first");
    }

    [Fact]
    public async Task GetFillups_UnknownAuto_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"mpg404-{Guid.NewGuid()}@test.com");

        var resp = await client.GetAsync("/api/autos/nonexistent-id/fillups");

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

        var resp = await client.GetAsync("/api/autos/x/fillups");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
