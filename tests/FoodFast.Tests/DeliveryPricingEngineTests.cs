using FoodFast.Core.Models;
using FoodFast.Core.Services;
using Xunit;

namespace FoodFast.Tests;

/// <summary>
/// Comprehensive test suite for the DeliveryPricingEngine.
/// 
/// This test suite demonstrates both Blackbox and Whitebox testing techniques
/// as part of the Software Testing course curriculum.
/// 
/// Testing Techniques Demonstrated:
/// 1. Happy Path Testing - Validates normal operation under expected conditions
/// 2. Blackbox Testing - Specification-based (Equivalence Partitioning, Boundary Value Analysis)
/// 3. Heuristics - Zero-One-Many, Goldilocks principle
/// 4. Whitebox Testing - Line, Branch, and Path Coverage
/// 
/// All tests follow the AAA (Arrange, Act, Assert) pattern.
/// </summary>
public class DeliveryPricingEngineTests
{
    private readonly DeliveryPricingEngine _sut; // System Under Test

    public DeliveryPricingEngineTests()
    {
        _sut = new DeliveryPricingEngine();
    }

    #region 1: THE HAPPY PATH

    /// <summary>
    /// Happy Path Test: Validates the system works flawlessly under normal, expected conditions.
    /// 
    /// Testing Technique: Happy Path Testing
    /// Purpose: Demonstrates that the core functionality works as intended when provided with
    ///          valid, typical inputs that fall within all expected ranges.
    /// 
    /// Scenario: A typical lunch order
    /// - Distance: 6 km (medium distance bucket)
    /// - Cart Subtotal: $30 (below free delivery threshold)
    /// - Rush Hour: No (standard pricing)
    /// 
    /// Expected Result: Base fee of $5.00 (medium distance), no rush hour surcharge, no free delivery
    /// </summary>
    [Fact]
    public void CalculateFee_HappyPath_TypicalLunchOrder_ReturnsExpectedFee()
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = 30.00m,
            DistanceInKm = 6.0,
            IsRushHour = false
        };
        const decimal expectedFee = 5.00m;

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    #endregion

    #region 2: BLACKBOX - SPECIFICATION-BASED TESTING

    /// <summary>
    /// Equivalence Partitioning Test: Tests one representative value from each distance bucket.
    /// 
    /// Testing Technique: Equivalence Partitioning (Blackbox)
    /// Purpose: Reduces test cases by testing one representative from each equivalence class.
    ///          If one value in a partition works, all values in that partition should work.
    /// 
    /// Partitions:
    /// 1. Short Distance (< 5.0 km): Base fee $2.00
    /// 2. Medium Distance (5.0 to < 10.0 km): Base fee $5.00
    /// 3. Long Distance (>= 10.0 km): Base fee $10.00
    /// </summary>
    [Theory]
    [InlineData(3.0, 2.00)]   // Short distance - expect $2.00
    [InlineData(7.5, 5.00)]   // Medium distance - expect $5.00
    [InlineData(15.0, 10.00)] // Long distance - expect $10.00
    public void CalculateFee_EquivalencePartitioning_DistanceBuckets_ReturnsCorrectBaseFee(
        double distanceInKm, decimal expectedFee)
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m, // Below free delivery threshold
            DistanceInKm = distanceInKm,
            IsRushHour = false
        };

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    /// <summary>
    /// Boundary Value Analysis Test: Tests the free delivery threshold at critical boundaries.
    /// 
    /// Testing Technique: Boundary Value Analysis (Blackbox)
    /// Purpose: Defects often occur at boundaries between equivalence classes.
    ///          Testing at, just below, and just above boundaries catches off-by-one errors.
    /// 
    /// Business Rule: "Free delivery if cart is $50.00 OR MORE"
    /// Implementation Bug: Uses '>' instead of '>=', causing failure at exactly $50.00
    /// 
    /// Test Cases:
    /// 1. $49.99 - Just below boundary (should pay fee) - PASS
    /// 2. $50.00 - AT boundary (should be free) - FAIL (intentional bug)
    /// 3. $50.01 - Just above boundary (should be free) - PASS
    /// </summary>
    [Theory]
    [InlineData(49.99, 5.00)]   // Just below $50 - should pay fee
    // 🔴 EXPECTED TO FAIL 🔴
    // The spec demands $0.00 fee at exactly $50.00. 
    // Because the developer wrote '>' instead of '>=', this test will catch the defect.
    [InlineData(50.00, 0.00)]   // At exactly $50 - should be FREE (BUG: will fail)
    [InlineData(50.01, 0.00)]   // Just above $50 - should be FREE
    public void CalculateFee_BoundaryValueAnalysis_FreeDeliveryThreshold_ReturnsCorrectFee(
        decimal cartSubtotal, decimal expectedFee)
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = cartSubtotal,
            DistanceInKm = 6.0, // Medium distance (base fee $5.00)
            IsRushHour = false
        };

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    #endregion

    #region 3: HEURISTICS - ZERO, ONE, MANY & GOLDILOCKS

    /// <summary>
    /// Zero-One-Many Heuristic Tests: Validates behavior with zero, one, and many elements.
    /// 
    /// Testing Technique: Zero-One-Many Heuristic
    /// Purpose: Many bugs occur at edge cases involving zero, one, or many items/distances.
    ///          This heuristic ensures the system handles these common edge cases correctly.
    /// 
    /// Test Cases:
    /// 1. Zero: Distance of 0.0 km (minimum valid distance)
    /// 2. One: Distance of 1.0 km (single unit)
    /// 3. Many: Distance of 25.0 km (larger value, still within range)
    /// </summary>
    [Fact]
    public void CalculateFee_ZeroOneMany_ZeroDistance_ReturnsBaseFee()
    {
        // Arrange - Zero distance (edge case: minimum valid distance)
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m,
            DistanceInKm = 0.0, // Zero - minimum valid distance
            IsRushHour = false
        };
        const decimal expectedFee = 2.00m;

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    [Fact]
    public void CalculateFee_ZeroOneMany_OneKilometer_ReturnsBaseFee()
    {
        // Arrange - One kilometer (single unit)
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m,
            DistanceInKm = 1.0, // One - single unit
            IsRushHour = false
        };
        const decimal expectedFee = 2.00m;

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    [Fact]
    public void CalculateFee_ZeroOneMany_ManyKilometers_ReturnsBaseFee()
    {
        // Arrange - Many kilometers (larger value)
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m,
            DistanceInKm = 25.0, // Many - larger value
            IsRushHour = false
        };
        const decimal expectedFee = 10.00m;

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    /// <summary>
    /// Goldilocks Heuristic Tests: Validates rejection of "too small" and "too big" values.
    /// 
    /// Testing Technique: Goldilocks Principle
    /// Purpose: Named after the fairy tale, this heuristic tests that the system rejects
    ///          values that are "too small" and "too big", while accepting values that are
    ///          "just right" (within valid range).
    /// 
    /// Test Cases:
    /// 1. Too Small: Negative distance (should throw ArgumentOutOfRangeException)
    /// 2. Too Big: Distance > 100 km (should throw InvalidOperationException)
    /// </summary>
    [Fact]
    public void CalculateFee_Goldilocks_TooSmall_NegativeDistance_ThrowsArgumentOutOfRangeException()
    {
        // Arrange - Too Small: Negative distance (invalid)
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m,
            DistanceInKm = -1.0, // Too Small - negative distance
            IsRushHour = false
        };

        // Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.CalculateFee(order));

        Assert.Contains("cannot be negative", exception.Message);
    }

    [Fact]
    public void CalculateFee_Goldilocks_TooBig_Exceeds100Km_ThrowsInvalidOperationException()
    {
        // Arrange - Too Big: Distance exceeds maximum supported
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m,
            DistanceInKm = 101.0, // Too Big - exceeds 100 km limit
            IsRushHour = false
        };

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => _sut.CalculateFee(order));

        Assert.Contains("exceeds the maximum supported distance", exception.Message);
    }

    #endregion

    #region 4: WHITEBOX - LINE, BRANCH, AND PATH COVERAGE

    /// <summary>
    /// Branch Coverage Test: Specifically exercises the True evaluation of the IsRushHour branch.
    /// 
    /// Testing Technique: Branch Coverage (Whitebox)
    /// Purpose: Ensures every branch (decision outcome) in the code is executed at least once.
    ///          This test specifically targets the IsRushHour = True path.
    /// 
    /// Scenario: Rush hour order with medium distance
    /// - Distance: 6 km (base fee $5.00)
    /// - Cart Subtotal: $30 (below free delivery)
    /// - Rush Hour: Yes (50% surcharge applies)
    /// 
    /// Expected Result: $5.00 * 1.5 = $7.50
    /// </summary>
    [Fact]
    public void CalculateFee_BranchCoverage_RushHourTrue_AppliesSurcharge()
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = 30.00m, // Below free delivery threshold
            DistanceInKm = 6.0,     // Medium distance - base fee $5.00
            IsRushHour = true      // Rush hour - 50% surcharge
        };
        const decimal expectedFee = 7.50m; // $5.00 * 1.5 = $7.50

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    /// <summary>
    /// Path Coverage Test: Exercises the "override path" where Free Delivery logic
    /// takes precedence over Rush Hour logic.
    /// 
    /// Testing Technique: Path Coverage (Whitebox)
    /// Purpose: Ensures all possible execution paths through the code are tested.
    ///          This test proves that the Free Delivery logic correctly overrides
    ///          the Rush Hour logic, regardless of previous calculations.
    /// 
    /// Execution Path:
    /// 1. Pass validation (distance is valid)
    /// 2. Calculate base fee based on distance
    /// 3. Apply rush hour multiplier (IsRushHour = True)
    /// 4. Override with free delivery (CartSubtotal > $50)
    /// 
    /// Scenario: High-value rush hour order
    /// - Distance: 6 km (base fee $5.00)
    /// - Cart Subtotal: $100.00 (qualifies for free delivery)
    /// - Rush Hour: Yes (would normally apply surcharge)
    /// 
    /// Expected Result: $0.00 (Free delivery overrides rush hour surcharge)
    /// 
    /// This test proves that:
    /// - The free delivery check happens AFTER the rush hour calculation
    /// - Free delivery is the final decision point (returns immediately)
    /// - The order of operations in the control flow is correct
    /// </summary>
    [Fact]
    public void CalculateFee_PathCoverage_RushHourWithFreeDelivery_FreeDeliveryOverridesSurcharge()
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = 100.00m, // Above $50 threshold - FREE delivery
            DistanceInKm = 6.0,      // Medium distance - base fee $5.00
            IsRushHour = true       // Rush hour - would normally be $7.50
        };
        const decimal expectedFee = 0.00m; // Free delivery overrides everything

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    #endregion

    #region 5: ADDITIONAL DISTANCE BOUNDARY TESTS

    /// <summary>
    /// Additional Boundary Value Analysis Tests: Tests exact distance boundaries.
    ///
    /// Testing Technique: Boundary Value Analysis (Blackbox)
    /// Purpose: Tests at exact boundary points for distance buckets.
    ///
    /// Test Cases:
    /// 1. 4.999 km - Just below 5.0 km boundary (short distance, $2.00)
    /// 2. 5.0 km - AT 5.0 km boundary (medium distance, $5.00)
    /// 3. 9.999 km - Just below 10.0 km boundary (medium distance, $5.00)
    /// 4. 10.0 km - AT 10.0 km boundary (long distance, $10.00)
    /// 5. 100.0 km - Maximum valid distance (long distance, $10.00)
    /// </summary>
    [Theory]
    [InlineData(4.999, 2.00)]   // Just below 5.0 km - short distance
    [InlineData(5.0, 5.00)]     // At 5.0 km boundary - medium distance
    [InlineData(9.999, 5.00)]   // Just below 10.0 km - medium distance
    [InlineData(10.0, 10.00)]   // At 10.0 km boundary - long distance
    [InlineData(100.0, 10.00)]  // Maximum valid distance - long distance
    public void CalculateFee_DistanceBoundaries_ReturnsCorrectFee(
        double distanceInKm, decimal expectedFee)
    {
        // Arrange
        var order = new DeliveryOrder
        {
            CartSubtotal = 25.00m, // Below free delivery threshold
            DistanceInKm = distanceInKm,
            IsRushHour = false
        };

        // Act
        decimal actualFee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(expectedFee, actualFee);
    }

    #endregion
}
