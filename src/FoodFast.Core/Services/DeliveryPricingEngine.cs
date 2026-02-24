using FoodFast.Core.Models;

namespace FoodFast.Core.Services;

/// <summary>
/// Calculates delivery fees based on distance, rush hour status, and cart subtotal.
/// </summary>
public class DeliveryPricingEngine
{
    /// <summary>
    /// Calculates the delivery fee for a given order.
    /// </summary>
    /// <param name="order">The delivery order containing cart subtotal, distance, and rush hour status</param>
    /// <returns>The calculated delivery fee</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when distance is negative</exception>
    /// <exception cref="InvalidOperationException">Thrown when distance exceeds 100 km</exception>
    public decimal CalculateFee(DeliveryOrder order)
    {
        // Input validation
        if (order.DistanceInKm < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(order.DistanceInKm),
                order.DistanceInKm,
                "Distance cannot be negative.");
        }

        if (order.DistanceInKm > 100)
        {
            throw new InvalidOperationException(
                $"Distance of {order.DistanceInKm} km exceeds the maximum supported distance of 100 km.");
        }

        // Calculate base fee based on distance
        decimal baseFee;

        if (order.DistanceInKm < 5.0)
        {
            baseFee = 2.00m;
        }
        else if (order.DistanceInKm < 10.0)
        {
            baseFee = 5.00m;
        }
        else
        {
            baseFee = 10.00m;
        }

        // Apply rush hour surcharge
        decimal calculatedFee = baseFee;

        if (order.IsRushHour)
        {
            calculatedFee = baseFee * 1.5m;
        }

        // Free delivery override
        // 🔴 INTENTIONAL BUG (SLIDE 17: BOUNDARY VALUE) 🔴
        // SPECIFICATION: "Free delivery if cart is $50.00 OR MORE" (>=)
        // IMPLEMENTATION: We use strictly greater-than (>) to cause a boundary failure.
        if (order.CartSubtotal > 50.00m)
        {
            return 0.00m;
        }

        return calculatedFee;
    }
}
