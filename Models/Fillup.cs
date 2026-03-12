public class Fillup
{
    public int Id { get; set; }
    public int AutoId { get; set; }
    public Auto Auto { get; set; } = null!;
    public DateTime FilledAt { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public FuelType FuelType { get; set; }
    public decimal PricePerGallon { get; set; }
    public decimal Gallons { get; set; }
    public decimal Odometer { get; set; }
    public bool IsPartialFill { get; set; }
}
