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

    public async Task SendMagicLinkAsync(string toEmail, string token, string baseUrl)
    {
        var link = $"{baseUrl.TrimEnd('/')}/auth/verify?token={token}";

        if (_client is null || _fromAddress is null)
        {
            // Dev mode: log the link so it can be used without email
            _logger.LogError("\u001b[1;31m╔══════════════════════════════════════════════════════╗\u001b[0m");
            _logger.LogError("\u001b[1;31m║  MAGIC LINK for {Email}\u001b[0m", toEmail);
            _logger.LogError("\u001b[1;31m║  {Link}\u001b[0m", link);
            _logger.LogError("\u001b[1;31m╚══════════════════════════════════════════════════════╝\u001b[0m");
            return;
        }

        var message = new EmailMessage(
            senderAddress: _fromAddress,
            recipients: new EmailRecipients([new(toEmail)]),
            content: new EmailContent("Sign in to Gasoholic")
            {
                PlainText = $"Click this link to sign in (expires in 24 hours):\n\n{link}\n\nIf you didn't request this, ignore this email.",
                Html = $"""
                    <p>Click the button below to sign in to Gasoholic. This link expires in 24 hours.</p>
                    <p><a href="{link}" style="display:inline-block;padding:12px 24px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;font-weight:600;">Sign in to Gasoholic</a></p>
                    <p style="color:#6b7280;font-size:14px;">Or copy this URL: {link}</p>
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
