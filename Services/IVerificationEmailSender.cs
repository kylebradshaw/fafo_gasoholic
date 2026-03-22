public interface IVerificationEmailSender
{
    Task SendMagicLinkAsync(string toEmail, string token, string baseUrl);
}
