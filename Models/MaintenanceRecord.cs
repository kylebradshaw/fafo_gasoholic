public class MaintenanceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AutoId { get; set; } = string.Empty;
    public MaintenanceType Type { get; set; }
    public DateTime PerformedAt { get; set; }
    public decimal Odometer { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
}
