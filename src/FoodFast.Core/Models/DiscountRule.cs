namespace FoodFast.Core.Models;

/// <summary>
/// Represents a discount rule that can be applied to an order.
/// 
/// ARCHITECTURAL CHOICE: Using a simple model class with clear properties.
/// This makes the domain model explicit and easy to test.
/// 
/// REAL-WORLD BEST PRACTICE: Domain models should be pure data structures
/// without business logic. Logic lives in services that operate on these models.
/// </summary>
public class DiscountRule
{
    /// <summary>
    /// Unique identifier for the discount rule.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the discount (e.g., "First Time Customer").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of discount: Percentage or FixedAmount.
    /// </summary>
    public DiscountType Type { get; set; }

    /// <summary>
    /// The discount value (percentage or fixed amount).
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Minimum order subtotal required to qualify for this discount.
    /// </summary>
    public decimal MinimumOrderAmount { get; set; }

    /// <summary>
    /// Whether this discount can be combined with other discounts.
    /// </summary>
    public bool CanStack { get; set; }
}

/// <summary>
/// Enumeration of discount types.
/// 
/// ARCHITECTURAL CHOICE: Using an enum instead of strings provides type safety
/// and prevents invalid values at compile time.
/// </summary>
public enum DiscountType
{
    /// <summary>
    /// Percentage-based discount (e.g., 10% off).
    /// Value represents the percentage (10 = 10%).
    /// </summary>
    Percentage,

    /// <summary>
    /// Fixed amount discount (e.g., $5 off).
    /// Value represents the dollar amount.
    /// </summary>
    FixedAmount
}
