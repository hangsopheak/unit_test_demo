using FoodFast.Core.Models;

namespace FoodFast.Core.Interfaces;

/// <summary>
/// Defines the contract for calculating discounts on orders.
/// 
/// ARCHITECTURAL CHOICE: Interface-based design enables:
/// - Dependency injection for testability
/// - Multiple implementations (e.g., different discount strategies)
/// - Easy mocking in unit tests
/// </summary>
public interface IDiscountCalculator
{
    /// <summary>
    /// Calculates the total discount amount for an order based on applicable rules.
    /// </summary>
    /// <param name="orderTotal">The original order total before discounts.</param>
    /// <param name="applicableRules">The discount rules that may apply to this order.</param>
    /// <returns>
    /// A list of discount results showing which rules were applied and the resulting totals.
    /// </returns>
    /// <remarks>
    /// This method demonstrates the TDD principle of designing the API
    /// based on how it will be used (test-first approach). The return type
    /// provides rich information for both the application and tests.
    /// </remarks>
    List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules);
}
