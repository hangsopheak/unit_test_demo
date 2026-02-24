namespace FoodFast.Core.Models;

/// <summary>
/// Data Transfer Object representing a delivery order.
/// </summary>
public class DeliveryOrder
{
    private decimal _cartSubtotal;

    /// <summary>
    /// Gets or sets the subtotal of items in the shopping cart.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
    public decimal CartSubtotal
    {
        get => _cartSubtotal;
        set => _cartSubtotal = value < 0
            ? throw new ArgumentOutOfRangeException(nameof(CartSubtotal), value, "Cart subtotal cannot be negative.")
            : value;
    }

    /// <summary>
    /// Gets or sets the delivery distance in kilometers.
    /// </summary>
    public double DistanceInKm { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the order is placed during rush hour.
    /// </summary>
    public bool IsRushHour { get; set; }
}
