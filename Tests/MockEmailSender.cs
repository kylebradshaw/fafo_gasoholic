public class MockEmailSender : IVerificationEmailSender
{
    public bool IsConfigured => false;
    public string? SenderDomain => null;
    public string? SenderAddress => null;

    // Track sent magic links for assertions in tests
    public List<(string Email, string Token, string BaseUrl)> SentMagicLinks { get; } = new();

    public Task SendMagicLinkAsync(string toEmail, string token, string baseUrl)
    {
        SentMagicLinks.Add((toEmail, token, baseUrl));
        return Task.CompletedTask;
    }

    public Task<string?> SendTestEmailAsync(string toEmail) => Task.FromResult<string?>(null);
}
