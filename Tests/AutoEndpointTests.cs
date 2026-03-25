using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Integration tests for the vehicle (auto) endpoints:
///   GET    /api/autos
///   POST   /api/autos
///   PUT    /api/autos/{id}
///   DELETE /api/autos/{id}
/// </summary>
public class AutoEndpointTests(GasoholicWebAppFactory factory) : IntegrationTestBase(factory)
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static object MakeAutoRequest(
        string brand = "Toyota", string model = "Camry",
        string plate = "ABC123", decimal odometer = 50000m) =>
        new { brand, model, plate, odometer };

    // ── GET /api/autos ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAutos_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.GetAsync("/api/autos");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetAutos_NewUser_ReturnsEmptyList()
    {
        var client = await CreateAuthenticatedClientAsync($"noautos-{Guid.NewGuid()}@test.com");

        var resp = await client.GetAsync("/api/autos");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal(0, doc.GetArrayLength());
    }

    [Fact]
    public async Task GetAutos_ReturnsOnlyOwnAutos()
    {
        var userAEmail = $"usera-{Guid.NewGuid()}@test.com";
        var userBEmail = $"userb-{Guid.NewGuid()}@test.com";
        var clientA = await CreateAuthenticatedClientAsync(userAEmail);
        var clientB = await CreateAuthenticatedClientAsync(userBEmail);

        // User A creates two autos
        await clientA.PostAsJsonAsync("/api/autos", MakeAutoRequest("Ford", "F-150", "AA111"));
        await clientA.PostAsJsonAsync("/api/autos", MakeAutoRequest("Honda", "Civic", "AA222"));
        // User B creates one auto
        await clientB.PostAsJsonAsync("/api/autos", MakeAutoRequest("Chevy", "Silverado", "BB111"));

        var respA = await clientA.GetAsync("/api/autos");
        var respB = await clientB.GetAsync("/api/autos");

        var docsA = (await ReadJsonAsync(respA)).EnumerateArray().ToList();
        var docsB = (await ReadJsonAsync(respB)).EnumerateArray().ToList();

        Assert.Equal(2, docsA.Count);
        Assert.Single(docsB);
    }

    [Fact]
    public async Task GetAutos_OrderedByBrandThenModel()
    {
        var client = await CreateAuthenticatedClientAsync($"order-{Guid.NewGuid()}@test.com");

        await client.PostAsJsonAsync("/api/autos", MakeAutoRequest("Toyota", "Camry", "T1"));
        await client.PostAsJsonAsync("/api/autos", MakeAutoRequest("Toyota", "Avalon", "T2"));
        await client.PostAsJsonAsync("/api/autos", MakeAutoRequest("BMW", "X5", "B1"));

        var resp = await client.GetAsync("/api/autos");
        var rows = (await ReadJsonAsync(resp)).EnumerateArray().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("BMW", rows[0].GetProperty("brand").GetString());
        Assert.Equal("Toyota", rows[1].GetProperty("brand").GetString());
        Assert.Equal("Avalon", rows[1].GetProperty("model").GetString());
        Assert.Equal("Toyota", rows[2].GetProperty("brand").GetString());
        Assert.Equal("Camry", rows[2].GetProperty("model").GetString());
    }

    // ── POST /api/autos ────────────────────────────────────────────────────────

    [Fact]
    public async Task PostAuto_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/api/autos", MakeAutoRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostAuto_ValidData_Returns201WithNewId()
    {
        var client = await CreateAuthenticatedClientAsync($"create-{Guid.NewGuid()}@test.com");

        var resp = await client.PostAsJsonAsync("/api/autos",
            MakeAutoRequest("Mazda", "CX-5", "MZD1", 12345m));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.True(doc.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Mazda", doc.GetProperty("brand").GetString());
        Assert.Equal("CX-5", doc.GetProperty("model").GetString());
        Assert.Equal("MZD1", doc.GetProperty("plate").GetString());
        Assert.Equal(12345m, doc.GetProperty("odometer").GetDecimal());
    }

    [Fact]
    public async Task PostAuto_AppearsInGetList()
    {
        var client = await CreateAuthenticatedClientAsync($"postget-{Guid.NewGuid()}@test.com");

        await client.PostAsJsonAsync("/api/autos", MakeAutoRequest("Subaru", "Outback", "SUB1"));

        var resp = await client.GetAsync("/api/autos");
        var rows = (await ReadJsonAsync(resp)).EnumerateArray().ToList();

        Assert.Single(rows);
        Assert.Equal("Subaru", rows[0].GetProperty("brand").GetString());
    }

    // ── PUT /api/autos/{id} ────────────────────────────────────────────────────

    [Fact]
    public async Task PutAuto_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.PutAsJsonAsync("/api/autos/1", MakeAutoRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PutAuto_ValidUpdate_ReturnsUpdatedData()
    {
        var client = await CreateAuthenticatedClientAsync($"put-{Guid.NewGuid()}@test.com");

        var createResp = await client.PostAsJsonAsync("/api/autos", MakeAutoRequest("Ford", "Escape", "OLD1"));
        var id = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var updateResp = await client.PutAsJsonAsync($"/api/autos/{id}",
            MakeAutoRequest("Ford", "Explorer", "NEW1", 99000m));

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var doc = await ReadJsonAsync(updateResp);
        Assert.Equal("Explorer", doc.GetProperty("model").GetString());
        Assert.Equal("NEW1", doc.GetProperty("plate").GetString());
        Assert.Equal(99000m, doc.GetProperty("odometer").GetDecimal());
    }

    [Fact]
    public async Task PutAuto_NonExistentId_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"put404-{Guid.NewGuid()}@test.com");

        var resp = await client.PutAsJsonAsync("/api/autos/99999", MakeAutoRequest());

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PutAuto_BelongingToOtherUser_Returns403()
    {
        var ownerClient = await CreateAuthenticatedClientAsync($"owner-{Guid.NewGuid()}@test.com");
        var otherClient = await CreateAuthenticatedClientAsync($"other-{Guid.NewGuid()}@test.com");

        var createResp = await ownerClient.PostAsJsonAsync("/api/autos", MakeAutoRequest());
        var id = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var resp = await otherClient.PutAsJsonAsync($"/api/autos/{id}", MakeAutoRequest("Hacked", "Car", "HACK"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── DELETE /api/autos/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAuto_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.DeleteAsync("/api/autos/1");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAuto_ValidId_Returns204AndRemovedFromList()
    {
        var client = await CreateAuthenticatedClientAsync($"delete-{Guid.NewGuid()}@test.com");

        var createResp = await client.PostAsJsonAsync("/api/autos", MakeAutoRequest());
        var id = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var deleteResp = await client.DeleteAsync($"/api/autos/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listResp = await client.GetAsync("/api/autos");
        var rows = (await ReadJsonAsync(listResp)).EnumerateArray().ToList();
        Assert.DoesNotContain(rows, r => r.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task DeleteAuto_NonExistentId_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync($"del404-{Guid.NewGuid()}@test.com");

        var resp = await client.DeleteAsync("/api/autos/99999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAuto_BelongingToOtherUser_Returns403()
    {
        var ownerClient = await CreateAuthenticatedClientAsync($"delowner-{Guid.NewGuid()}@test.com");
        var otherClient = await CreateAuthenticatedClientAsync($"delother-{Guid.NewGuid()}@test.com");

        var createResp = await ownerClient.PostAsJsonAsync("/api/autos", MakeAutoRequest());
        var id = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        var resp = await otherClient.DeleteAsync($"/api/autos/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteAuto_CascadesFillupsDelete()
    {
        var client = await CreateAuthenticatedClientAsync($"cascade-{Guid.NewGuid()}@test.com");

        // Create auto
        var createResp = await client.PostAsJsonAsync("/api/autos", MakeAutoRequest());
        var autoId = (await ReadJsonAsync(createResp)).GetProperty("id").GetInt32();

        // Add a fillup to the auto
        await client.PostAsJsonAsync($"/api/autos/{autoId}/fillups", new
        {
            filledAt = DateTime.UtcNow,
            location = (string?)null,
            latitude = (double?)null,
            longitude = (double?)null,
            fuelType = 0,       // FuelType.Regular = 0
            pricePerGallon = 3.50m,
            gallons = 10m,
            odometer = 10000m,
            isPartialFill = false
        });

        // Delete auto
        await client.DeleteAsync($"/api/autos/{autoId}");

        // The auto is gone, GET should return 404
        var fillupResp = await client.GetAsync($"/api/autos/{autoId}/fillups");
        Assert.Equal(HttpStatusCode.NotFound, fillupResp.StatusCode);
    }
}
