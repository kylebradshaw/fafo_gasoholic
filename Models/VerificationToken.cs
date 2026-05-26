public class VerificationToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;  // 6-digit numeric login code
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }             // 30 min after creation
    public DateTime? UsedAt { get; set; }               // null = unused
    public int Attempts { get; set; }                   // wrong-code submissions; locks at 5
}
