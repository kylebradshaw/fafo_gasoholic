public class MockEmailSender : IVerificationEmailSender
{
    public bool IsConfigured => false;
    public string? SenderDomain => null;
    public string? SenderAddress => null;

    // Track sent login codes for assertions in tests
    public List<(string Email, string Code)> SentLoginCodes { get; } = new();

    public Task SendLoginCodeAsync(string toEmail, string code)
    {
        SentLoginCodes.Add((toEmail, code));
        return Task.CompletedTask;
    }

    public Task<string?> SendTestEmailAsync(string toEmail) => Task.FromResult<string?>(null);
}
