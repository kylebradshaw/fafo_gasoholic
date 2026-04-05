public class MaintenanceRecord
{
    public int Id { get; set; }
    public int AutoId { get; set; }
    public Auto Auto { get; set; } = null!;
    public MaintenanceType Type { get; set; }
    public DateTime PerformedAt { get; set; }
    public decimal Odometer { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
}
