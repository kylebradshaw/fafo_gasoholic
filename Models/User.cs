public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; } = true;  // Default true; migration verifies existing rows
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSignIn { get; set; }
    public DateTime? LastInteraction { get; set; }
}
