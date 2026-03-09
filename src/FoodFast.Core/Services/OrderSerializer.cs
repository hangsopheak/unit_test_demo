using System.Globalization;
using FoodFast.Core.Models;

namespace FoodFast.Core.Services;

/// <summary>
/// Serializes and deserializes DeliveryOrder objects to a pipe-delimited string format.
/// Demonstrates the Inverse (Round-Trip) PBT pattern: Deserialize(Serialize(order)) == order.
/// </summary>
public static class OrderSerializer
{
    /// <summary>
    /// Converts a DeliveryOrder to a pipe-delimited string.
    /// Format: "CartSubtotal|DistanceInKm|IsRushHour"
    /// </summary>
    public static string Serialize(DeliveryOrder order) =>
        string.Join("|",
            order.CartSubtotal.ToString(CultureInfo.InvariantCulture),
            order.DistanceInKm.ToString(CultureInfo.InvariantCulture),
            order.IsRushHour.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Reconstructs a DeliveryOrder from a pipe-delimited string produced by Serialize.
    /// </summary>
    public static DeliveryOrder Deserialize(string data)
    {
        var parts = data.Split('|');
        return new DeliveryOrder
        {
            CartSubtotal = decimal.Parse(parts[0], CultureInfo.InvariantCulture),
            DistanceInKm = double.Parse(parts[1],  CultureInfo.InvariantCulture),
            IsRushHour   = bool.Parse(parts[2])
        };
    }
}
