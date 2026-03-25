using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Integration tests for the authentication endpoints:
///   POST /auth/login
///   GET  /auth/verify
///   POST /auth/resend
///   POST /auth/logout
///   GET  /auth/me
/// </summary>
public class AuthEndpointTests(GasoholicWebAppFactory factory) : IntegrationTestBase(factory)
{
    // ── /auth/login ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_EmptyEmail_Returns400()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/login", new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("Email is required", doc.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Login_WhitespaceEmail_Returns400()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/login", new { email = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_NewEmail_CreatesUserAndReturnsPending()
    {
        var client = CreateClient();
        var email = $"newuser-{Guid.NewGuid()}@test.com";

        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        // 202 Accepted with status "pending"
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("pending", doc.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Login_NewEmail_SendsMagicLinkEmail()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"magic-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Single(mockSender.SentMagicLinks, m => m.Email == email);
    }

    [Fact]
    public async Task Login_ExistingUnverifiedUserWithActiveToken_ReturnsPendingWithoutResend()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"pending-{Guid.NewGuid()}@test.com";

        // First login creates user + sends token
        await client.PostAsJsonAsync("/auth/login", new { email });
        var countBefore = mockSender.SentMagicLinks.Count;

        // Second login — user is still unverified and has an active token
        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        // No new email should have been sent
        Assert.Equal(countBefore, mockSender.SentMagicLinks.Count);
    }

    [Fact]
    public async Task Login_AlreadyVerifiedUser_SetsSessionAndReturnsOk()
    {
        var client = CreateClient();
        var email = $"verified-{Guid.NewGuid()}@test.com";

        // Use dev-login to create a pre-verified user
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await client.SendAsync(devReq);

        // Now login as that verified user
        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal(email, doc.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_NormalizesEmailToLowercase()
    {
        var client = CreateClient();
        var email = $"UpperCase-{Guid.NewGuid()}@TEST.COM";
        var mockSender = GetMockEmailSender();

        await client.PostAsJsonAsync("/auth/login", new { email });

        var sent = mockSender.SentMagicLinks.LastOrDefault();
        Assert.Equal(email.ToLowerInvariant(), sent.Email);
    }

    // ── /auth/verify ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidToken_RedirectsToApp()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-ok-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var token = mockSender.SentMagicLinks.Last().Token;

        var resp = await client.GetAsync($"/auth/verify?token={token}");

        // Expect redirect to /app.html (we set AllowAutoRedirect = false)
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/app.html", resp.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Verify_InvalidToken_Returns400WithError()
    {
        var client = CreateClient();

        var resp = await client.GetAsync("/auth/verify?token=nonexistenttoken");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("token_not_found", doc.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Verify_AlreadyUsedToken_Returns400()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-used-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var token = mockSender.SentMagicLinks.Last().Token;

        // First verify — should succeed
        await client.GetAsync($"/auth/verify?token={token}");

        // Second verify with the same token
        var resp = await client.GetAsync($"/auth/verify?token={token}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("token_used", doc.GetProperty("error").GetString());
    }

    // ── /auth/resend ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Resend_EmptyEmail_Returns400()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Resend_NonExistentEmail_Returns200ToAvoidUserEnumeration()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/resend",
            new { email = $"ghost-{Guid.NewGuid()}@test.com" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Resend_AlreadyVerifiedEmail_Returns200ToAvoidUserEnumeration()
    {
        var client = CreateClient();
        var email = $"resend-verified-{Guid.NewGuid()}@test.com";

        // Create a pre-verified user via dev-login
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await client.SendAsync(devReq);

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Resend_ValidUnverifiedEmail_SendsNewToken()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"resend-valid-{Guid.NewGuid()}@test.com";

        // Login to create the user
        await client.PostAsJsonAsync("/auth/login", new { email });
        var countBefore = mockSender.SentMagicLinks.Count;

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(countBefore + 1, mockSender.SentMagicLinks.Count);
    }

    [Fact]
    public async Task Resend_ExceedsRateLimit_Returns429()
    {
        var client = CreateClient();
        var email = $"ratelimit-{Guid.NewGuid()}@test.com";

        // Initial login counts as token #1
        await client.PostAsJsonAsync("/auth/login", new { email });
        // Resends #2, #3, #4 should succeed
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });

        // The 5th request (4th resend) should be rate-limited
        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("too_many_requests", doc.GetProperty("error").GetString());
    }

    // ── /auth/logout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ClearsSessionAndReturns200()
    {
        var client = await CreateAuthenticatedClientAsync($"logout-{Guid.NewGuid()}@test.com");

        // Verify authenticated
        var meBefore = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        await client.PostAsync("/auth/logout", null);

        // After logout, /auth/me should return 401
        var meAfter = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Fact]
    public async Task Logout_WhenNotLoggedIn_StillReturns200()
    {
        var client = CreateClient();

        var resp = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── /auth/me ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        var client = CreateClient();

        var resp = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_Authenticated_ReturnsEmail()
    {
        var email = $"me-ok-{Guid.NewGuid()}@test.com";
        var client = await CreateAuthenticatedClientAsync(email);

        var resp = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal(email, doc.GetProperty("email").GetString());
    }
}
