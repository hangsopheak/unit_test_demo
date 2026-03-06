using FoodFast.Core.Interfaces;
using FoodFast.Core.Models;
using FoodFast.Core.Services;
using Xunit;

namespace FoodFast.Tests;

/// <summary>
/// TDD Demonstration: Building DiscountCalculator from scratch.
/// 
/// This test suite demonstrates the Red-Green-Refactor cycle in action.
/// Each test was written BEFORE any implementation code existed.
/// 
/// TDD PRINCIPLES DEMONSTRATED:
/// 1. Write a failing test first (RED)
/// 2. Write minimal code to pass (GREEN)
/// 3. Refactor to improve design (REFACTOR)
/// 
/// TESTING TECHNIQUES:
/// - AAA Pattern (Arrange, Act, Assert)
/// - Descriptive test names that document business rules
/// - One assertion per test for clarity
/// - Test isolation (each test is independent)
/// </summary>
public class DiscountCalculatorTests
{
    private readonly IDiscountCalculator _sut;

    public DiscountCalculatorTests()
    {
        // System Under Test
        _sut = new DiscountCalculator();
    }

    #region TDD Cycle 1: No Discounts (Baseline)

    /// <summary>
    /// TDD CYCLE 1 - RED: First test written before any implementation.
    /// 
    /// BUSINESS RULE: When no discount rules apply, return empty list.
    /// 
    /// WHY START HERE: Establishes the baseline behavior. Every feature
    /// needs a "do nothing" case. This is the simplest possible scenario.
    /// 
    /// TDD PRINCIPLE: Start with the simplest case to get quick feedback.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WhenNoRulesApply_ReturnsEmptyList()
    {
        // Arrange
        decimal orderTotal = 50.00m;
        var applicableRules = new List<DiscountRule>();

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region TDD Cycle 2: Percentage Discount

    /// <summary>
    /// TDD CYCLE 2 - RED: Test for percentage-based discount.
    /// 
    /// BUSINESS RULE: 10% off orders over $50.
    /// 
    /// SCENARIO: $100 order with 10% discount = $10 off = $90 total.
    /// 
    /// TDD PRINCIPLE: Add one feature at a time. This test focuses
    /// only on percentage discounts, ignoring fixed amounts and stacking.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WithPercentageDiscount_AppliesCorrectAmount()
    {
        // Arrange
        decimal orderTotal = 100.00m;
        var rule = new DiscountRule
        {
            Id = "PERC10",
            Name = "10% Off",
            Type = DiscountType.Percentage,
            Value = 10m,
            MinimumOrderAmount = 50.00m,
            CanStack = false
        };
        var applicableRules = new List<DiscountRule> { rule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Single(result);
        Assert.Equal(rule, result[0].Rule);
        Assert.Equal(10.00m, result[0].DiscountAmount); // 10% of $100
        Assert.Equal(90.00m, result[0].NewTotal); // $100 - $10
    }

    #endregion

    #region TDD Cycle 3: Fixed Amount Discount

    /// <summary>
    /// TDD CYCLE 3 - RED: Test for fixed-amount discount.
    /// 
    /// BUSINESS RULE: $5 off for first-time customers.
    /// 
    /// SCENARIO: $30 order with $5 discount = $25 total.
    /// 
    /// TDD PRINCIPLE: Test one discount type at a time. This ensures
    /// each type works correctly before combining them.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WithFixedAmountDiscount_AppliesCorrectAmount()
    {
        // Arrange
        decimal orderTotal = 30.00m;
        var rule = new DiscountRule
        {
            Id = "FIX5",
            Name = "$5 Off First Order",
            Type = DiscountType.FixedAmount,
            Value = 5.00m,
            MinimumOrderAmount = 0.00m,
            CanStack = false
        };
        var applicableRules = new List<DiscountRule> { rule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Single(result);
        Assert.Equal(rule, result[0].Rule);
        Assert.Equal(5.00m, result[0].DiscountAmount);
        Assert.Equal(25.00m, result[0].NewTotal);
    }

    #endregion

    #region TDD Cycle 4: Minimum Order Amount

    /// <summary>
    /// TDD CYCLE 4 - RED: Test for minimum order amount requirement.
    /// 
    /// BUSINESS RULE: Discount only applies if order meets minimum threshold.
    /// 
    /// SCENARIO: $30 order with discount requiring $50 minimum = no discount.
    /// 
    /// TDD PRINCIPLE: Test minimum order validation. This ensures eligibility
    /// rules are enforced correctly.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WhenOrderBelowMinimum_DoesNotApplyDiscount()
    {
        // Arrange
        decimal orderTotal = 30.00m;
        var rule = new DiscountRule
        {
            Id = "PERC10",
            Name = "10% Off",
            Type = DiscountType.Percentage,
            Value = 10m,
            MinimumOrderAmount = 50.00m,
            CanStack = false
        };
        var applicableRules = new List<DiscountRule> { rule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region TDD Cycle 5: Stacking Discounts

    /// <summary>
    /// TDD CYCLE 5 - RED: Test for stacking multiple discounts.
    /// 
    /// BUSINESS RULE: Multiple discounts can combine if marked as stackable.
    /// 
    /// SCENARIO: $100 order with 10% off + $5 off = $15 total discount = $85.
    /// 
    /// TDD PRINCIPLE: Test combinations after individual features work.
    /// This ensures complex scenarios are handled correctly.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WithStackableDiscounts_AppliesAll()
    {
        // Arrange
        decimal orderTotal = 100.00m;
        var percentageRule = new DiscountRule
        {
            Id = "PERC10",
            Name = "10% Off",
            Type = DiscountType.Percentage,
            Value = 10m,
            MinimumOrderAmount = 50.00m,
            CanStack = true
        };
        var fixedRule = new DiscountRule
        {
            Id = "FIX5",
            Name = "$5 Off",
            Type = DiscountType.FixedAmount,
            Value = 5.00m,
            MinimumOrderAmount = 0.00m,
            CanStack = true
        };
        var applicableRules = new List<DiscountRule> { percentageRule, fixedRule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Equal(2, result.Count);

        // First discount: 10% of $100 = $10 off
        Assert.Equal(percentageRule, result[0].Rule);
        Assert.Equal(10.00m, result[0].DiscountAmount);
        Assert.Equal(90.00m, result[0].NewTotal);

        // Second discount: $5 off (applied to $90)
        Assert.Equal(fixedRule, result[1].Rule);
        Assert.Equal(5.00m, result[1].DiscountAmount);
        Assert.Equal(85.00m, result[1].NewTotal);
    }

    /// <summary>
    /// TDD CYCLE 5 - NON-STACKING: Test that non-stackable discounts stop processing.
    /// 
    /// BUSINESS RULE: Non-stackable discounts prevent further discounts.
    /// 
    /// SCENARIO: Two discounts, first is non-stackable = only first applies.
    /// 
    /// TDD PRINCIPLE: Test both positive and negative cases. Ensure
    /// system correctly enforces business rules in both directions.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WithNonStackableDiscount_OnlyAppliesFirst()
    {
        // Arrange
        decimal orderTotal = 100.00m;
        var firstRule = new DiscountRule
        {
            Id = "PERC20",
            Name = "20% Off",
            Type = DiscountType.Percentage,
            Value = 20m,
            MinimumOrderAmount = 100.00m,
            CanStack = false // Stops stacking
        };
        var secondRule = new DiscountRule
        {
            Id = "FIX5",
            Name = "$5 Off",
            Type = DiscountType.FixedAmount,
            Value = 5.00m,
            MinimumOrderAmount = 0.00m,
            CanStack = true
        };
        var applicableRules = new List<DiscountRule> { firstRule, secondRule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Single(result); // Only first discount applied
        Assert.Equal(firstRule, result[0].Rule);
        Assert.Equal(20.00m, result[0].DiscountAmount);
    }

    #endregion

    #region TDD Cycle 6: Negative Total Protection

    /// <summary>
    /// TDD CYCLE 6 - RED: Test protection against negative totals.
    /// 
    /// BUSINESS RULE: Discounts cannot make the order total negative.
    /// 
    /// SCENARIO: $10 order with $15 discount = $0 total (not -$5).
    /// 
    /// TDD PRINCIPLE: Test edge cases and error conditions. This
    /// prevents business logic errors that could cause financial losses.
    /// </summary>
    [Fact]
    public void CalculateDiscounts_WhenDiscountExceedsOrderTotal_CapsAtZero()
    {
        // Arrange
        decimal orderTotal = 10.00m;
        var rule = new DiscountRule
        {
            Id = "FIX15",
            Name = "$15 Off",
            Type = DiscountType.FixedAmount,
            Value = 15.00m,
            MinimumOrderAmount = 0.00m,
            CanStack = false
        };
        var applicableRules = new List<DiscountRule> { rule };

        // Act
        var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

        // Assert
        Assert.Single(result);
        Assert.Equal(rule, result[0].Rule);
        Assert.Equal(15.00m, result[0].DiscountAmount);
        Assert.Equal(0.00m, result[0].NewTotal); // Capped at zero
    }

    #endregion
}
