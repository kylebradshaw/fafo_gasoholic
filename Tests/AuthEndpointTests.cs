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
    public async Task Login_NewEmail_CreatesUserAndReturnsPendingVerification()
    {
        var client = CreateClient();
        var email = $"newuser-{Guid.NewGuid()}@test.com";

        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        // 202 Accepted with status "pending_verification" (new user, first-time verification)
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("pending_verification", doc.GetProperty("status").GetString());
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
    public async Task Login_VerifiedUserWithActiveSession_ReturnsOkWithoutSendingEmail()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verified-session-{Guid.NewGuid()}@test.com";

        // Use dev-login to create a pre-verified user with an active session on this client
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await client.SendAsync(devReq);

        var countBefore = mockSender.SentMagicLinks.Count;

        // Login on the SAME client (same session cookie) — should return OK without sending email
        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal(email, doc.GetProperty("email").GetString());
        Assert.Equal(countBefore, mockSender.SentMagicLinks.Count);
    }

    [Fact]
    public async Task Login_VerifiedUserWithoutSession_SendsMagicLinkAndReturnsPendingReauth()
    {
        // Bootstrap: create a verified user via dev-login on one client
        var setupClient = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verified-reauth-{Guid.NewGuid()}@test.com";

        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        var countBefore = mockSender.SentMagicLinks.Count;

        // A FRESH client (no session cookie) tries to log in as the verified user
        var freshClient = CreateClient();
        var resp = await freshClient.PostAsJsonAsync("/auth/login", new { email });

        // Should require re-auth — magic link sent, 202 pending_reauth
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("pending_reauth", doc.GetProperty("status").GetString());
        Assert.Equal(countBefore + 1, mockSender.SentMagicLinks.Count);
        Assert.Equal(email, mockSender.SentMagicLinks.Last().Email);
    }

    [Fact]
    public async Task Login_FullReauthLifecycle_RequiresMagicLinkAfterLogout()
    {
        var mockSender = GetMockEmailSender();
        var email = $"reauth-lifecycle-{Guid.NewGuid()}@test.com";

        // 1. New user logs in → pending_verification + magic link
        var client = CreateClient();
        var login1 = await client.PostAsJsonAsync("/auth/login", new { email });
        Assert.Equal(HttpStatusCode.Accepted, login1.StatusCode);
        Assert.Equal("pending_verification", (await ReadJsonAsync(login1)).GetProperty("status").GetString());

        // 2. Click magic link → verified, session established
        var token1 = mockSender.SentMagicLinks.Last(m => m.Email == email).Token;
        var verify1 = await client.GetAsync($"/auth/verify?token={token1}");
        Assert.Equal(HttpStatusCode.Redirect, verify1.StatusCode);

        // 3. /auth/me works
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);

        // 4. Logout
        await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);

        // 5. Login again (same client, no session) → must get a new magic link as pending_reauth
        var login2 = await client.PostAsJsonAsync("/auth/login", new { email });
        Assert.Equal(HttpStatusCode.Accepted, login2.StatusCode);
        Assert.Equal("pending_reauth", (await ReadJsonAsync(login2)).GetProperty("status").GetString());

        // 6. New token must be different from the first (the first was already used)
        var token2 = mockSender.SentMagicLinks.Last(m => m.Email == email).Token;
        Assert.NotEqual(token1, token2);

        // 7. Click new magic link → session restored
        var verify2 = await client.GetAsync($"/auth/verify?token={token2}");
        Assert.Equal(HttpStatusCode.Redirect, verify2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);
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

    // ── magic-link origin ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_MagicLinkBaseUrl_UsesOriginHeader()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"origin-{Guid.NewGuid()}@test.com";

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "Origin", "http://localhost:4200" } }
        };
        await client.SendAsync(request);

        var sent = mockSender.SentMagicLinks.Last();
        Assert.Equal("http://localhost:4200", sent.BaseUrl);
    }

    [Fact]
    public async Task Login_MagicLinkBaseUrl_FallsBackToRequestHost_WhenNoOrigin()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"noorigin-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });

        var sent = mockSender.SentMagicLinks.Last();
        // Should fall back to the request host (the test server's address)
        Assert.StartsWith("http://", sent.BaseUrl);
        Assert.DoesNotContain("4200", sent.BaseUrl);
    }

    [Fact]
    public async Task Resend_MagicLinkBaseUrl_UsesOriginHeader()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"resend-origin-{Guid.NewGuid()}@test.com";

        // Create user first
        await client.PostAsJsonAsync("/auth/login", new { email });

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/resend")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "Origin", "http://localhost:4200" } }
        };
        await client.SendAsync(request);

        var sent = mockSender.SentMagicLinks.Last();
        Assert.Equal("http://localhost:4200", sent.BaseUrl);
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

        // Expect redirect to /app (we set AllowAutoRedirect = false)
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/app", resp.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Verify_ValidToken_SessionWorksForApiCalls()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-session-{Guid.NewGuid()}@test.com";

        // Login to create user + token
        await client.PostAsJsonAsync("/auth/login", new { email });
        var token = mockSender.SentMagicLinks.Last().Token;

        // Verify — sets session cookie on the same client
        var verifyResp = await client.GetAsync($"/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResp.StatusCode);

        // The same client (same cookie jar) should now be authenticated
        var meResp = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        // And should be able to create an auto (the exact scenario that failed)
        var autoResp = await client.PostAsJsonAsync("/api/autos",
            new { brand = "Toyota", model = "Highlander", plate = "BJK715", odometer = 93005m });
        Assert.Equal(HttpStatusCode.Created, autoResp.StatusCode);
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
    public async Task Resend_VerifiedUserMidReauth_SendsNewMagicLink()
    {
        var mockSender = GetMockEmailSender();
        var email = $"resend-reauth-{Guid.NewGuid()}@test.com";

        // Bootstrap a verified user (dev-login on a separate client so we don't carry a session)
        var setupClient = CreateClient();
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        // Fresh client logs in → triggers a re-auth magic link (verified user, no session)
        var client = CreateClient();
        await client.PostAsJsonAsync("/auth/login", new { email });
        var countAfterLogin = mockSender.SentMagicLinks.Count;

        // Now resend should work because the user has an active unused token
        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(countAfterLogin + 1, mockSender.SentMagicLinks.Count);
    }

    [Fact]
    public async Task Resend_VerifiedUserMidReauth_RateLimited()
    {
        var email = $"resend-reauth-rl-{Guid.NewGuid()}@test.com";

        // Bootstrap verified user
        var setupClient = CreateClient();
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        var client = CreateClient();
        // Token #1 from re-auth login
        await client.PostAsJsonAsync("/auth/login", new { email });
        // Resends #2, #3, #4 succeed
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });

        // 5th total token request → rate limited
        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
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
