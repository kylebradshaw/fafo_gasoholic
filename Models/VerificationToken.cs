public class VerificationToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;  // GUID, unique
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }             // 24hr after creation
    public DateTime? UsedAt { get; set; }               // null = unused
}
