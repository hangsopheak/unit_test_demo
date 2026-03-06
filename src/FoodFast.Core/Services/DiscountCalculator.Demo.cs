using FoodFast.Core.Interfaces;
using FoodFast.Core.Models;

namespace FoodFast.Core.Services;

/// <summary>
/// TDD DEMO VERSION: Progressive implementation for live coding demonstrations.
/// 
/// HOW TO USE THIS FILE FOR LIVE DEMO:
/// 1. Start with only Cycle 1 enabled (uncomment Cycle 1 region)
/// 2. Run tests - they should fail
/// 3. Ask students what needs to be implemented
/// 4. Uncomment the implementation for Cycle 1
/// 5. Run tests - they should pass
/// 6. Move to Cycle 2 and repeat
/// 
/// Each region represents one TDD cycle (Red-Green-Refactor).
/// Uncomment regions progressively to demonstrate TDD in action.
/// </summary>
public class DiscountCalculatorDemo
{
    #region TDD CYCLE 1: No Discounts (Baseline)

    /// <summary>
    /// CYCLE 1 IMPLEMENTATION: Returns empty list when no rules apply.
    /// 
    /// Uncomment this method to implement Cycle 1.
    /// This is the simplest possible implementation - just return empty list.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        // RED: Test expects empty list
        // GREEN: Simplest implementation that passes
        return new List<DiscountResult>();
    }
    */

    #endregion

    #region TDD CYCLE 2: Percentage Discount

    /// <summary>
    /// CYCLE 2 IMPLEMENTATION: Adds percentage discount calculation.
    /// 
    /// Uncomment this method to implement Cycle 2.
    /// Now we need to calculate percentage discounts.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        var applications = new List<DiscountResult>();
        
        foreach (var rule in applicableRules)
        {
            // Calculate percentage discount
            if (rule.Type == DiscountType.Percentage)
            {
                decimal discountAmount = orderTotal * (rule.Value / 100m);
                decimal newTotal = orderTotal - discountAmount;
                
                applications.Add(new DiscountResult
                {
                    Rule = rule,
                    DiscountAmount = discountAmount,
                    NewTotal = newTotal
                });
            }
        }
        
        return applications;
    }
    */

    #endregion

    #region TDD CYCLE 3: Fixed Amount Discount

    /// <summary>
    /// CYCLE 3 IMPLEMENTATION: Adds fixed amount discount calculation.
    /// 
    /// Uncomment this method to implement Cycle 3.
    /// Now we handle both percentage and fixed amount discounts.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        var applications = new List<DiscountResult>();
        
        foreach (var rule in applicableRules)
        {
            decimal discountAmount = rule.Type switch
            {
                DiscountType.Percentage => orderTotal * (rule.Value / 100m),
                DiscountType.FixedAmount => rule.Value,
                _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
            };
            
            decimal newTotal = orderTotal - discountAmount;
            
            applications.Add(new DiscountResult
            {
                Rule = rule,
                DiscountAmount = discountAmount,
                NewTotal = newTotal
            });
        }
        
        return applications;
    }
    */

    #endregion

    #region TDD CYCLE 4: Minimum Order Amount

    /// <summary>
    /// CYCLE 4 IMPLEMENTATION: Adds minimum order amount validation.
    /// 
    /// Uncomment this method to implement Cycle 4.
    /// Discounts only apply if order meets minimum threshold.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        var applications = new List<DiscountResult>();
        
        foreach (var rule in applicableRules)
        {
            // Check minimum order amount
            if (orderTotal < rule.MinimumOrderAmount)
                continue;
            
            decimal discountAmount = rule.Type switch
            {
                DiscountType.Percentage => orderTotal * (rule.Value / 100m),
                DiscountType.FixedAmount => rule.Value,
                _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
            };
            
            decimal newTotal = orderTotal - discountAmount;
            
            applications.Add(new DiscountResult
            {
                Rule = rule,
                DiscountAmount = discountAmount,
                NewTotal = newTotal
            });
        }
        
        return applications;
    }
    */

    #endregion

    #region TDD CYCLE 5: Stacking Discounts

    /// <summary>
    /// CYCLE 5 IMPLEMENTATION: Adds discount stacking support.
    /// 
    /// Uncomment this method to implement Cycle 5.
    /// Multiple discounts can apply if marked as stackable.
    /// Each discount applies to the reduced total from previous discounts.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        var applications = new List<DiscountResult>();
        decimal currentTotal = orderTotal;
        
        foreach (var rule in applicableRules)
        {
            // Check minimum order amount
            if (currentTotal < rule.MinimumOrderAmount)
                continue;
            
            decimal discountAmount = rule.Type switch
            {
                DiscountType.Percentage => currentTotal * (rule.Value / 100m),
                DiscountType.FixedAmount => rule.Value,
                _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
            };
            
            currentTotal -= discountAmount;
            
            applications.Add(new DiscountResult
            {
                Rule = rule,
                DiscountAmount = discountAmount,
                NewTotal = currentTotal
            });
            
            // Stop if discount doesn't stack
            if (!rule.CanStack)
                break;
        }
        
        return applications;
    }
    */

    #endregion

    #region TDD CYCLE 6: Negative Total Protection

    /// <summary>
    /// CYCLE 6 IMPLEMENTATION: Adds protection against negative totals.
    /// 
    /// Uncomment this method to implement Cycle 6 (FINAL).
    /// Order total cannot become negative after applying discounts.
    /// </summary>
    /*
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        var applications = new List<DiscountResult>();
        decimal currentTotal = orderTotal;
        
        foreach (var rule in applicableRules)
        {
            // Check minimum order amount
            if (currentTotal < rule.MinimumOrderAmount)
                continue;
            
            decimal discountAmount = rule.Type switch
            {
                DiscountType.Percentage => currentTotal * (rule.Value / 100m),
                DiscountType.FixedAmount => rule.Value,
                _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
            };
            
            currentTotal -= discountAmount;
            
            // Cap at zero to prevent negative totals
            if (currentTotal < 0)
                currentTotal = 0;
            
            applications.Add(new DiscountResult
            {
                Rule = rule,
                DiscountAmount = discountAmount,
                NewTotal = currentTotal
            });
            
            // Stop if discount doesn't stack
            if (!rule.CanStack)
                break;
        }
        
        return applications;
    }
    */

    #endregion
}
