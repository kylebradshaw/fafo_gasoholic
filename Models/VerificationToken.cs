public class VerificationToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;  // GUID, unique
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }             // 24hr after creation
    public DateTime? UsedAt { get; set; }               // null = unused
}
