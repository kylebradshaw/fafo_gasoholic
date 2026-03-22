using Azure;
using Azure.Communication.Email;

public class VerificationEmailSender : IVerificationEmailSender
{
    private readonly EmailClient? _client;
    private readonly string? _fromAddress;
    private readonly ILogger<VerificationEmailSender> _logger;

    public VerificationEmailSender(IConfiguration config, ILogger<VerificationEmailSender> logger)
    {
        _logger = logger;
        var connStr = config.GetConnectionString("ACS");
        if (!string.IsNullOrEmpty(connStr))
        {
            _client = new EmailClient(connStr);
            // ACS managed domain sender address — format: DoNotReply@<domain>
            var domain = config["AcsSenderDomain"] ?? "azurecomm.net";
            _fromAddress = $"DoNotReply@{domain}";
        }
    }

    public async Task SendMagicLinkAsync(string toEmail, string token, string baseUrl)
    {
        var link = $"{baseUrl.TrimEnd('/')}/auth/verify?token={token}";

        if (_client is null || _fromAddress is null)
        {
            // Dev mode: log the link so it can be used without email
            _logger.LogWarning("ACS not configured — magic link for {Email}: {Link}", toEmail, link);
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
}
