using FsCheck;
using FsCheck.Xunit;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

namespace FoodFast.Tests;

/// <summary>
/// Property-Based Testing demonstration using FsCheck.
///
/// Contrasts example-based testing (specific inputs) with property-based testing
/// (universal rules verified across hundreds of generated inputs).
///
/// Three parts:
/// 1. Invariant properties on DeliveryPricingEngine
/// 2. Test Oracle pattern — naive vs real engine
/// 3. Stateful PBT — OrderBasket with random action sequences
/// </summary>
public class PropertyBasedTests
{
    private readonly DeliveryPricingEngine _sut = new();

    // Generates valid DeliveryOrder values within the engine's accepted range
    private static Arbitrary<DeliveryOrder> GenValidOrder() =>
        Arb.From(
            from subtotal in Gen.Choose(0, 20000).Select(n => (decimal)n / 100)  // $0–$200
            from distance in Gen.Choose(0, 10000).Select(n => n / 100.0)         // 0–100 km
            from rushHour in Arb.Generate<bool>()
            select new DeliveryOrder
            {
                CartSubtotal = subtotal,
                DistanceInKm = distance,
                IsRushHour   = rushHour
            });

    #region The Contrast — Example vs Property

    // Example-based: one specific scenario
    [Fact]
    public void CalculateFee_ExampleBased_ReturnsExpectedFee()
    {
        // Arrange
        var order = new DeliveryOrder { CartSubtotal = 30m, DistanceInKm = 6.0, IsRushHour = false };

        // Act
        decimal fee = _sut.CalculateFee(order);

        // Assert
        Assert.Equal(5.00m, fee);
    }

    #endregion

    #region Part 1: Invariant Properties

    // Rule: fee is always >= 0 for any valid order
    [Property]
    public Property CalculateFee_ForAnyValidOrder_FeeIsNeverNegative() =>
        Prop.ForAll(GenValidOrder(), order =>
            _sut.CalculateFee(order) >= 0);

    // Rule: fee never exceeds $15 (long distance $10 × 1.5 rush hour surcharge)
    [Property]
    public Property CalculateFee_ForAnyValidOrder_FeeNeverExceedsMaximum() =>
        Prop.ForAll(GenValidOrder(), order =>
            _sut.CalculateFee(order) <= 15.00m);

    // Rule: when cart > $50, fee is always $0 regardless of distance or rush hour
    [Property]
    public Property CalculateFee_WhenCartAbove50_FeeIsAlwaysZero()
    {
        var richOrders =
            Arb.From(
                from distance in Gen.Choose(0, 10000).Select(n => n / 100.0)
                from rushHour in Arb.Generate<bool>()
                select new DeliveryOrder
                {
                    CartSubtotal = 50.01m,
                    DistanceInKm = distance,
                    IsRushHour   = rushHour
                });

        return Prop.ForAll(richOrders, order =>
            _sut.CalculateFee(order) == 0.00m);
    }

    // Idempotence: pure function — same input always produces same result
    [Property]
    public Property CalculateFee_CalledTwiceWithSameInput_ReturnsSameResult() =>
        Prop.ForAll(GenValidOrder(), order =>
            _sut.CalculateFee(order) == _sut.CalculateFee(order));

    #endregion

    #region Part 2: Test Oracle

    // Naive reference: obviously correct, no abstraction, used as the trusted oracle
    private static decimal SimpleCalculateFee(DeliveryOrder order)
    {
        if (order.CartSubtotal > 50m) return 0m;

        decimal baseFee = order.DistanceInKm switch
        {
            < 5.0  => 2.00m,
            < 10.0 => 5.00m,
            _      => 10.00m
        };
        return order.IsRushHour ? baseFee * 1.5m : baseFee;
    }

    // Oracle property: real engine must match naive version for every valid input
    [Property]
    public Property CalculateFee_AlwaysMatchesSimpleReferenceImplementation() =>
        Prop.ForAll(GenValidOrder(), order =>
            _sut.CalculateFee(order) == SimpleCalculateFee(order));

    #endregion

    #region Part 3: Inverse Pattern — OrderSerializer

    // Pattern 1: decode(encode(x)) == x — no data is lost or corrupted
    [Property]
    public Property Serialize_ThenDeserialize_ReturnsOriginalOrder() =>
        Prop.ForAll(GenValidOrder(), order =>
        {
            var roundTripped = OrderSerializer.Deserialize(OrderSerializer.Serialize(order));
            return roundTripped.CartSubtotal == order.CartSubtotal
                && roundTripped.DistanceInKm == order.DistanceInKm
                && roundTripped.IsRushHour   == order.IsRushHour;
        });

    #endregion

    #region Part 4: Commutativity Pattern — DiscountCalculator

    // Pattern 3: f(g(x)) == g(f(x)) — two stackable fixed discounts give the same total in any order
    [Property]
    public Property CalculateDiscounts_TwoStackableFixedDiscounts_OrderDoesNotAffectFinalTotal()
    {
        var gen =
            from total   in Gen.Choose(1000, 20000).Select(n => (decimal)n / 100)
            from amountA in Gen.Choose(10, 500).Select(n => (decimal)n / 100)
            from amountB in Gen.Choose(10, 500).Select(n => (decimal)n / 100)
            select (total, amountA, amountB);

        return Prop.ForAll(Arb.From(gen), t =>
        {
            var (total, amountA, amountB) = t;
            var calculator = new DiscountCalculator();

            var ruleA = new DiscountRule { Id = "A", Name = "Discount A", Type = DiscountType.FixedAmount, Value = amountA, MinimumOrderAmount = 0, CanStack = true };
            var ruleB = new DiscountRule { Id = "B", Name = "Discount B", Type = DiscountType.FixedAmount, Value = amountB, MinimumOrderAmount = 0, CanStack = true };

            var resultsAB = calculator.CalculateDiscounts(total, new List<DiscountRule> { ruleA, ruleB });
            var resultsBA = calculator.CalculateDiscounts(total, new List<DiscountRule> { ruleB, ruleA });

            decimal finalAB = resultsAB.Any() ? resultsAB.Last().NewTotal : total;
            decimal finalBA = resultsBA.Any() ? resultsBA.Last().NewTotal : total;

            return finalAB == finalBA;
        });
    }

    #endregion

    #region Part 5: Stateful PBT — OrderBasket

    private abstract record BasketAction;
    private record Add(decimal Price) : BasketAction;
    private record RemoveLast         : BasketAction;
    private record ClearAll           : BasketAction;

    [Fact]
    public void OrderBasket_AfterAnySequenceOfActions_TotalMatchesModel()
    {
        // Generator: random sequences of basket actions
        var actionGen = Gen.OneOf(
            Gen.Choose(50, 5000).Select(n => (BasketAction)new Add((decimal)n / 100)),
            Gen.Constant((BasketAction)new RemoveLast()),
            Gen.Constant((BasketAction)new ClearAll()));

        var property = Prop.ForAll(Arb.From(Gen.ListOf(actionGen)), actions =>
        {
            var basket = new OrderBasket();
            var model  = new List<decimal>();   // trusted reference: a plain list

            foreach (var action in actions)
            {
                switch (action)
                {
                    case Add(var price):
                        basket.AddItem(price);
                        model.Add(price);
                        break;
                    case RemoveLast:
                        if (model.Count > 0)
                        {
                            basket.RemoveLastItem();
                            model.RemoveAt(model.Count - 1);
                        }
                        break;
                    case ClearAll:
                        basket.Clear();
                        model.Clear();
                        break;
                }

                // Verify invariant after every single action
                if (basket.Total != model.Sum()) return false;
            }
            return true;
        });

        property.QuickCheckThrowOnFailure();
    }

    #endregion
}
