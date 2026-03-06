namespace FoodFast.Core.Models;

/// <summary>
/// Represents the result of applying a discount to an order.
/// 
/// ARCHITECTURAL CHOICE: Separate result object allows us to track
/// which discounts were applied and why, which is valuable for:
/// - Customer-facing receipts
/// - Analytics and reporting
/// - Debugging discount calculations
/// </summary>
public class DiscountResult
{
    /// <summary>
    /// The discount rule that was applied.
    /// </summary>
    public DiscountRule Rule { get; set; } = null!;

    /// <summary>
    /// The amount discounted from the order.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// The order total after applying this discount.
    /// </summary>
    public decimal NewTotal { get; set; }
}
