using FoodFast.Api.Data;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

namespace FoodFast.Api.Models;

public record CreateOrderRequest(string CustomerName, decimal CartSubtotal, double DistanceInKm, bool IsRushHour);

public class OrderResponse
{
    public int      Id           { get; set; }
    public string   CustomerName { get; set; } = string.Empty;
    public decimal  CartSubtotal { get; set; }
    public double   DistanceInKm { get; set; }
    public bool     IsRushHour   { get; set; }
    public decimal  DeliveryFee  { get; set; }
    public DateTime CreatedAt    { get; set; }

    public static OrderResponse FromEntity(OrderEntity entity, DeliveryPricingEngine engine)
    {
        var order = new DeliveryOrder
        {
            CartSubtotal = entity.CartSubtotal,
            DistanceInKm = entity.DistanceInKm,
            IsRushHour   = entity.IsRushHour
        };
        return new OrderResponse
        {
            Id           = entity.Id,
            CustomerName = entity.CustomerName,
            CartSubtotal = entity.CartSubtotal,
            DistanceInKm = entity.DistanceInKm,
            IsRushHour   = entity.IsRushHour,
            DeliveryFee  = engine.CalculateFee(order),
            CreatedAt    = entity.CreatedAt
        };
    }
}
