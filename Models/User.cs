public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public ICollection<Auto> Autos { get; set; } = new List<Auto>();
}
