using FoodFast.Core.Models;

namespace FoodFast.Core.Services;

/// <summary>
/// Calculates delivery fees based on distance, rush hour status, and cart subtotal.
/// </summary>
public class DeliveryPricingEngine
{
    // Constants for pricing tiers - easily configurable and maintainable
    // Note: Using double for distance to match DeliveryOrder.DistanceInKm type
    private const double ShortDistanceThresholdKm = 5.0;
    private const double MediumDistanceThresholdKm = 10.0;

    private const decimal ShortDistanceFee = 2.00m;
    private const decimal MediumDistanceFee = 5.00m;
    private const decimal LongDistanceFee = 10.00m;

    private const decimal RushHourSurchargeMultiplier = 1.5m;
    private const decimal FreeDeliveryThreshold = 50.00m;

    private const double MaximumDeliveryDistanceKm = 100.0;

    /// <summary>
    /// Calculates the delivery fee for a given order.
    /// </summary>
    /// <param name="order">The delivery order containing cart subtotal, distance, and rush hour status</param>
    /// <returns>The calculated delivery fee</returns>
    /// <exception cref="ArgumentNullException">Thrown when order is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when distance is negative</exception>
    /// <exception cref="InvalidOperationException">Thrown when distance exceeds maximum supported distance</exception>
    public decimal CalculateFee(DeliveryOrder order)
    {
        // Validate input is not null
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order), "Order cannot be null.");
        }

        // Input validation for distance
        ValidateDistance(order.DistanceInKm);

        // Calculate base fee based on distance using a structured approach
        decimal baseFee = CalculateBaseFee(order.DistanceInKm);

        // Apply rush hour surcharge
        decimal calculatedFee = ApplyRushHourSurcharge(baseFee, order.IsRushHour);

        // Apply free delivery override
        return ApplyFreeDeliveryOverride(calculatedFee, order.CartSubtotal);
    }

    /// <summary>
    /// Validates the delivery distance.
    /// </summary>
    /// <param name="distanceInKm">The distance in kilometers to validate</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when distance is negative</exception>
    /// <exception cref="InvalidOperationException">Thrown when distance exceeds maximum supported distance</exception>
    private void ValidateDistance(double distanceInKm)
    {
        if (distanceInKm < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceInKm),
                distanceInKm,
                "Distance cannot be negative.");
        }

        if (distanceInKm > MaximumDeliveryDistanceKm)
        {
            throw new InvalidOperationException(
                $"Distance of {distanceInKm} km exceeds the maximum supported distance of {MaximumDeliveryDistanceKm} km.");
        }
    }

    /// <summary>
    /// Calculates the base delivery fee based on distance.
    /// Uses a pattern matching switch expression for clarity and maintainability.
    /// </summary>
    /// <param name="distanceInKm">The delivery distance in kilometers</param>
    /// <returns>The base delivery fee</returns>
    private decimal CalculateBaseFee(double distanceInKm)
    {
        return distanceInKm switch
        {
            < ShortDistanceThresholdKm => ShortDistanceFee,
            < MediumDistanceThresholdKm => MediumDistanceFee,
            _ => LongDistanceFee
        };
    }

    /// <summary>
    /// Applies rush hour surcharge if applicable.
    /// </summary>
    /// <param name="baseFee">The base delivery fee</param>
    /// <param name="isRushHour">Whether the order is during rush hour</param>
    /// <returns>The fee with rush hour surcharge applied if applicable</returns>
    private decimal ApplyRushHourSurcharge(decimal baseFee, bool isRushHour)
    {
        return isRushHour
            ? baseFee * RushHourSurchargeMultiplier
            : baseFee;
    }

    /// <summary>
    /// Applies free delivery override if cart subtotal meets threshold.
    /// </summary>
    /// <param name="calculatedFee">The calculated delivery fee</param>
    /// <param name="cartSubtotal">The cart subtotal amount</param>
    /// <returns>The final delivery fee (0 if free delivery applies)</returns>
    private decimal ApplyFreeDeliveryOverride(decimal calculatedFee, decimal cartSubtotal)
    {

        return cartSubtotal > FreeDeliveryThreshold
            ? 0.00m
            : calculatedFee;
    }

}
