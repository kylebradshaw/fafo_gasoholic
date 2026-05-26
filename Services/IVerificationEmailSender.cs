public interface IVerificationEmailSender
{
    Task SendLoginCodeAsync(string toEmail, string code);
    bool IsConfigured { get; }
    string? SenderDomain { get; }
    string? SenderAddress { get; }
    Task<string?> SendTestEmailAsync(string toEmail);
}
