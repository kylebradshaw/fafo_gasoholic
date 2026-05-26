using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Integration tests for the authentication endpoints:
///   POST /auth/login
///   POST /auth/verify
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

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("pending_verification", doc.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Login_NewEmail_SendsLoginCodeEmail()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"code-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });

        var sent = Assert.Single(mockSender.SentLoginCodes, m => m.Email == email);
        Assert.Matches("^[0-9]{6}$", sent.Code);
    }

    [Fact]
    public async Task Login_ExistingUnverifiedUserWithActiveToken_ReturnsPendingWithoutResend()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"pending-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var countBefore = mockSender.SentLoginCodes.Count;

        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(countBefore, mockSender.SentLoginCodes.Count);
    }

    [Fact]
    public async Task Login_VerifiedUserWithActiveSession_ReturnsOkWithoutSendingEmail()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verified-session-{Guid.NewGuid()}@test.com";

        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await client.SendAsync(devReq);

        var countBefore = mockSender.SentLoginCodes.Count;

        var resp = await client.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal(email, doc.GetProperty("email").GetString());
        Assert.Equal(countBefore, mockSender.SentLoginCodes.Count);
    }

    [Fact]
    public async Task Login_VerifiedUserWithoutSession_SendsCodeAndReturnsPendingReauth()
    {
        var setupClient = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verified-reauth-{Guid.NewGuid()}@test.com";

        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        var countBefore = mockSender.SentLoginCodes.Count;

        var freshClient = CreateClient();
        var resp = await freshClient.PostAsJsonAsync("/auth/login", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("pending_reauth", doc.GetProperty("status").GetString());
        Assert.Equal(countBefore + 1, mockSender.SentLoginCodes.Count);
        Assert.Equal(email, mockSender.SentLoginCodes.Last().Email);
    }

    [Fact]
    public async Task Login_FullReauthLifecycle_RequiresCodeAfterLogout()
    {
        var mockSender = GetMockEmailSender();
        var email = $"reauth-lifecycle-{Guid.NewGuid()}@test.com";

        // 1. New user logs in → pending_verification + code
        var client = CreateClient();
        var login1 = await client.PostAsJsonAsync("/auth/login", new { email });
        Assert.Equal(HttpStatusCode.Accepted, login1.StatusCode);
        Assert.Equal("pending_verification", (await ReadJsonAsync(login1)).GetProperty("status").GetString());

        // 2. Submit code → verified, session established
        var code1 = mockSender.SentLoginCodes.Last(m => m.Email == email).Code;
        var verify1 = await client.PostAsJsonAsync("/auth/verify", new { email, code = code1 });
        Assert.Equal(HttpStatusCode.OK, verify1.StatusCode);

        // 3. /auth/me works
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);

        // 4. Logout
        await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);

        // 5. Login again → new pending_reauth code
        var login2 = await client.PostAsJsonAsync("/auth/login", new { email });
        Assert.Equal(HttpStatusCode.Accepted, login2.StatusCode);
        Assert.Equal("pending_reauth", (await ReadJsonAsync(login2)).GetProperty("status").GetString());

        // 6. New code must differ from the first (the first was already used)
        var code2 = mockSender.SentLoginCodes.Last(m => m.Email == email).Code;
        Assert.NotEqual(code1, code2);

        // 7. Submit new code → session restored
        var verify2 = await client.PostAsJsonAsync("/auth/verify", new { email, code = code2 });
        Assert.Equal(HttpStatusCode.OK, verify2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_NormalizesEmailToLowercase()
    {
        var client = CreateClient();
        var email = $"UpperCase-{Guid.NewGuid()}@TEST.COM";
        var mockSender = GetMockEmailSender();

        await client.PostAsJsonAsync("/auth/login", new { email });

        var sent = mockSender.SentLoginCodes.LastOrDefault();
        Assert.Equal(email.ToLowerInvariant(), sent.Email);
    }

    // ── /auth/verify ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_ValidCode_Returns200AndStartsSession()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-ok-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var code = mockSender.SentLoginCodes.Last().Code;

        var resp = await client.PostAsJsonAsync("/auth/verify", new { email, code });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal(email, doc.GetProperty("email").GetString());

        // Same client cookie is now authenticated
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Verify_ValidCode_SessionWorksForApiCalls()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-session-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var code = mockSender.SentLoginCodes.Last().Code;

        var verifyResp = await client.PostAsJsonAsync("/auth/verify", new { email, code });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

        var autoResp = await client.PostAsJsonAsync("/api/autos",
            new { brand = "Toyota", model = "Highlander", plate = "BJK715", odometer = 93005m });
        Assert.Equal(HttpStatusCode.Created, autoResp.StatusCode);
    }

    [Fact]
    public async Task Verify_UnknownEmail_Returns400()
    {
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/verify",
            new { email = $"ghost-{Guid.NewGuid()}@test.com", code = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("invalid_code", doc.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Verify_WrongCode_Returns400AndIncrementsAttempts()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-wrong-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var realCode = mockSender.SentLoginCodes.Last().Code;
        var wrongCode = realCode == "000000" ? "999999" : "000000";

        var resp = await client.PostAsJsonAsync("/auth/verify", new { email, code = wrongCode });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("invalid_code", doc.GetProperty("error").GetString());
        Assert.Equal(4, doc.GetProperty("attemptsRemaining").GetInt32());
    }

    [Fact]
    public async Task Verify_FiveWrongCodes_LocksToken()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-locked-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var realCode = mockSender.SentLoginCodes.Last().Code;
        var wrongCode = realCode == "000000" ? "999999" : "000000";

        for (int i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/auth/verify", new { email, code = wrongCode });

        // Even the correct code now fails because the token is locked / used
        var resp = await client.PostAsJsonAsync("/auth/verify", new { email, code = realCode });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Contains(doc.GetProperty("error").GetString(),
            new[] { "too_many_attempts", "code_expired" });
    }

    [Fact]
    public async Task Verify_AlreadyUsedCode_Returns400()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"verify-used-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var code = mockSender.SentLoginCodes.Last().Code;

        var first = await client.PostAsJsonAsync("/auth/verify", new { email, code });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var resp = await client.PostAsJsonAsync("/auth/verify", new { email, code });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var doc = await ReadJsonAsync(resp);
        Assert.Equal("code_expired", doc.GetProperty("error").GetString());
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
    public async Task Resend_VerifiedUserMidReauth_SendsNewCode()
    {
        var mockSender = GetMockEmailSender();
        var email = $"resend-reauth-{Guid.NewGuid()}@test.com";

        var setupClient = CreateClient();
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        var client = CreateClient();
        await client.PostAsJsonAsync("/auth/login", new { email });
        var countAfterLogin = mockSender.SentLoginCodes.Count;

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(countAfterLogin + 1, mockSender.SentLoginCodes.Count);
    }

    [Fact]
    public async Task Resend_VerifiedUserMidReauth_RateLimited()
    {
        var email = $"resend-reauth-rl-{Guid.NewGuid()}@test.com";

        var setupClient = CreateClient();
        var devReq = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-login")
        {
            Content = JsonContent.Create(new { email }),
            Headers = { { "X-Smoke-Test-Secret", GasoholicWebAppFactory.TestSecret } }
        };
        await setupClient.SendAsync(devReq);

        var client = CreateClient();
        await client.PostAsJsonAsync("/auth/login", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
    }

    [Fact]
    public async Task Resend_ValidUnverifiedEmail_SendsNewCode()
    {
        var client = CreateClient();
        var mockSender = GetMockEmailSender();
        var email = $"resend-valid-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        var countBefore = mockSender.SentLoginCodes.Count;

        var resp = await client.PostAsJsonAsync("/auth/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(countBefore + 1, mockSender.SentLoginCodes.Count);
    }

    [Fact]
    public async Task Resend_ExceedsRateLimit_Returns429()
    {
        var client = CreateClient();
        var email = $"ratelimit-{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/auth/login", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });
        await client.PostAsJsonAsync("/auth/resend", new { email });

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

        var meBefore = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        await client.PostAsync("/auth/logout", null);

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
