public class Auto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public decimal Odometer { get; set; }
    public ICollection<Fillup> Fillups { get; set; } = new List<Fillup>();
}
