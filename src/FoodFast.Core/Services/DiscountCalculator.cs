using FoodFast.Core.Interfaces;
using FoodFast.Core.Models;

namespace FoodFast.Core.Services;

/// <summary>
/// Calculates discounts for food delivery orders.
/// 
/// TDD DEVELOPMENT: This class was built test-first. Each method exists
/// because a test required it. The design emerged from the tests.
/// 
/// ARCHITECTURAL CHOICES DRIVEN BY TDD:
/// - Pure function design: No side effects, same input = same output
/// - Explicit return types: Rich result objects for verification
/// - Single Responsibility: Each method has one clear purpose
/// 
/// REAL-WORLD BEST PRACTICE: Pure functions are easier to test, reason about,
/// and maintain. They avoid hidden state and make dependencies explicit.
/// </summary>
public class DiscountCalculator : IDiscountCalculator
{
    /// <summary>
    /// Calculates applicable discounts for an order.
    /// 
    /// TDD IMPLEMENTATION: This method started as a simple return statement
    /// and evolved as more tests were added. Each test drove a new feature.
    /// 
    /// DESIGN EVOLUTION:
    /// 1. Initially: return new List<DiscountResult>();
    /// 2. After percentage test: Added percentage calculation logic
    /// 3. After fixed amount test: Added fixed amount logic
    /// 4. After stacking test: Added combination logic
    /// 5. After minimum amount test: Added eligibility check
    /// 6. After negative total test: Added validation
    /// </summary>
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        // TDD: Start simple, add complexity as tests demand it
        var applications = new List<DiscountResult>();
        decimal currentTotal = orderTotal; // Track running total for stacking

        // Process each applicable rule
        foreach (var rule in applicableRules)
        {
            // Check if order qualifies for this discount
            if (currentTotal < rule.MinimumOrderAmount)
                continue;

            // Calculate discount amount based on type
            decimal discountAmount = CalculateDiscountAmount(currentTotal, rule);

            // Apply discount and track the application
            currentTotal -= discountAmount;

            // Ensure total doesn't go negative
            if (currentTotal < 0)
                currentTotal = 0;

            applications.Add(new DiscountResult
            {
                Rule = rule,
                DiscountAmount = discountAmount,
                NewTotal = currentTotal
            });

            // If discount doesn't stack, stop after first one
            if (!rule.CanStack)
                break;
        }

        return applications;
    }

    /// <summary>
    /// Calculates the discount amount for a single rule.
    /// 
    /// TDD: Extracted to separate method during refactoring.
    /// This keeps the main method clean and makes each piece testable.
    /// 
    /// REFACTORING BENEFIT: Small, focused methods are easier to
    /// understand, test, and reuse.
    /// </summary>
    private decimal CalculateDiscountAmount(decimal orderTotal, DiscountRule rule)
    {
        return rule.Type switch
        {
            DiscountType.Percentage => orderTotal * (rule.Value / 100m),
            DiscountType.FixedAmount => rule.Value,
            _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
        };
    }
}
