using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Base class providing helpers shared across all integration test classes.
/// Each test class gets its own <see cref="GasoholicWebAppFactory"/> with a fresh in-memory database.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<GasoholicWebAppFactory>
{
    protected readonly GasoholicWebAppFactory Factory;

    protected IntegrationTestBase(GasoholicWebAppFactory factory)
    {
        Factory = factory;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that persists cookies across requests.
    /// </summary>
    protected HttpClient CreateClient() =>
        Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that is already authenticated as <paramref name="email"/>.
    /// Uses the /auth/dev-login smoke test endpoint, which requires SMOKE_TEST_SECRET.
    /// </summary>
    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "user@test.com")
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>
    /// Deserializes a JSON response body into a <see cref="JsonElement"/> for flexible assertions.
    /// </summary>
    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    /// <summary>
    /// Gets the <see cref="MockEmailSender"/> so tests can inspect captured tokens.
    /// </summary>
    protected MockEmailSender GetMockEmailSender() =>
        (MockEmailSender)Factory.Services.GetRequiredService<IVerificationEmailSender>();
}
