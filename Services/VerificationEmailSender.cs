using Azure;
using Azure.Communication.Email;

public class VerificationEmailSender : IVerificationEmailSender
{
    private readonly EmailClient? _client;
    private readonly string? _fromAddress;
    private readonly string? _senderDomain;
    private readonly ILogger<VerificationEmailSender> _logger;

    public VerificationEmailSender(IConfiguration config, ILogger<VerificationEmailSender> logger)
    {
        _logger = logger;
        var connStr = config.GetConnectionString("ACS");
        if (!string.IsNullOrEmpty(connStr))
        {
            _client = new EmailClient(connStr);
            // Custom domain sender address — format: verify@<domain>
            _senderDomain = config["AcsSenderDomain"] ?? "azurecomm.net";
            _fromAddress = $"verify@{_senderDomain}";
        }
    }

    public bool IsConfigured => _client is not null;
    public string? SenderDomain => _senderDomain;
    public string? SenderAddress => _fromAddress;

    public async Task SendLoginCodeAsync(string toEmail, string code)
    {
        if (_client is null || _fromAddress is null)
        {
            // Dev mode: log the code so it can be used without email
            _logger.LogError("[1;31m╔══════════════════════════════════════════════════════╗[0m");
            _logger.LogError("[1;31m║  LOGIN CODE for {Email}[0m", toEmail);
            _logger.LogError("[1;31m║  {Code}[0m", code);
            _logger.LogError("[1;31m╚══════════════════════════════════════════════════════╝[0m");
            return;
        }

        var message = new EmailMessage(
            senderAddress: _fromAddress,
            recipients: new EmailRecipients([new(toEmail)]),
            content: new EmailContent("Your Gasoholic sign-in code")
            {
                PlainText = $"Your Gasoholic sign-in code is:\n\n{code}\n\nEnter it in the app to sign in. This code expires in 30 minutes.\n\nIf you didn't request this, ignore this email.",
                Html = $"""
                    <p>Your Gasoholic sign-in code is:</p>
                    <p style="font-size:28px;font-weight:700;letter-spacing:0.15em;color:#111827;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;margin:16px 0;">{code}</p>
                    <p style="color:#6b7280;font-size:14px;">Enter it in the app to sign in. This code expires in 30 minutes.</p>
                    <p style="color:#6b7280;font-size:14px;">If you didn't request this, ignore this email.</p>
                    """
            }
        );

        await _client.SendAsync(WaitUntil.Completed, message);
    }

    public async Task<string?> SendTestEmailAsync(string toEmail)
    {
        if (_client is null || _fromAddress is null)
        {
            _logger.LogWarning("ACS not configured — test email to {Email} skipped", toEmail);
            return null;
        }

        var message = new EmailMessage(
            senderAddress: _fromAddress,
            recipients: new EmailRecipients([new(toEmail)]),
            content: new EmailContent("Gasoholic Test Email")
            {
                PlainText = "This is a test email from Gasoholic to verify ACS custom domain configuration.",
                Html = "<p>This is a test email from Gasoholic to verify ACS custom domain configuration.</p>"
            }
        );

        var result = await _client.SendAsync(WaitUntil.Completed, message);
        return result.Id;
    }
}
