public interface IVerificationEmailSender
{
    Task SendMagicLinkAsync(string toEmail, string token, string baseUrl);
    bool IsConfigured { get; }
    string? SenderDomain { get; }
    string? SenderAddress { get; }
    Task<string?> SendTestEmailAsync(string toEmail);
}
