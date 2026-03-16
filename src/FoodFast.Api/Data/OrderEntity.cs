namespace FoodFast.Api.Data;

/// <summary>
/// Database entity for delivery orders. Separate from the domain model
/// because the DB has its own concerns (auto-increment ID, timestamps).
/// </summary>
public class OrderEntity
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal CartSubtotal { get; set; }
    public double DistanceInKm { get; set; }
    public bool IsRushHour { get; set; }
    public DateTime CreatedAt { get; set; }
}
