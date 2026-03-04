using FoodFast.Core.Fakes;
using FoodFast.Core.Interfaces;
using FoodFast.Core.Services;
using Moq;

namespace FoodFast.Tests;

/// <summary>
/// PHASE 2: THE SOLUTION - Test Suite Demonstrating Test Doubles
/// 
/// This test suite demonstrates the four main types of test doubles:
/// 1. Dummy: Objects that are passed around but never actually used
/// 2. Stub: Objects that provide canned answers to calls made during the test
/// 3. Mock: Objects pre-programmed with expectations which form a specification of the calls they are expected to receive
/// 4. Fake: Objects that have working implementations, but usually take some shortcut which makes them unsuitable for production
/// </summary>
public class OrderDispatcherTests
{
    #region THE DUMMY AND THE STUB

    /// <summary>
    /// Demonstrates the use of Dummy and Stub test doubles.
    /// 
    /// DUMMY: An object that is passed to fill a parameter slot but is never actually used.
    /// - Used here: dummyLogger and dummyPayment are just "extras" to satisfy constructor parameters
    /// - No setup needed, no verification performed
    /// - They exist only to allow instantiation of the System Under Test (SUT)
    /// 
    /// STUB: An object that provides canned answers to calls made during the test.
    /// - Used here: stubInventory is set up to return false for IsInStock
    /// - Provides scripted input to force a specific logic path
    /// - No verification performed on the stub itself
    /// </summary>
    [Fact]
    public void DispatchOrder_WhenItemIsOutOfStock_ReturnsFalse()
    {
        // Dummy: Pass Mock<ILogger>().Object to the constructor. Add a comment that it is just an "Extra" to satisfy parameters.
        var dummyLogger = new Mock<ILogger>().Object;
        var dummyPayment = new Mock<IExternalPaymentApi>().Object;

        // Stub: Use Mock.Setup to force IsInStock to return false. Add a comment that Stubs provide scripted input.
        var stubInventory = new Mock<IInventoryService>();
        stubInventory.Setup(x => x.IsInStock(It.IsAny<string>())).Returns(false);

        var dispatcher = new OrderDispatcher(dummyLogger, stubInventory.Object, dummyPayment);

        var result = dispatcher.DispatchOrder("Pizza", 10.00m, "C001");

        // Assert it returns false.
        Assert.False(result);
    }

    #endregion

    #region THE MOCK (Behavior Verification)

    /// <summary>
    /// Demonstrates the use of Mock test doubles for behavior verification.
    /// 
    /// MOCK: An object that acts as a spy, recording all interactions for later verification.
    /// - Used here: mockPaymentApi is used to verify that ChargeCard was called
    /// - Mocks act as Spies to verify side-effects (Interactions)
    /// - Focuses on HOW the SUT behaves, not WHAT it returns
    /// - Ideal for verifying that dependencies are called correctly
    /// </summary>
    [Fact]
    public void DispatchOrder_WhenItemIsInStock_ShouldCallChargeCard()
    {
        var dummyLogger = new Mock<ILogger>().Object;
        var stubInventory = new Mock<IInventoryService>();
        stubInventory.Setup(x => x.IsInStock(It.IsAny<string>())).Returns(true);

        // Mock: Used to verify interaction (behavior)
        var mockPaymentApi = new Mock<IExternalPaymentApi>();

        var dispatcher = new OrderDispatcher(dummyLogger, stubInventory.Object, mockPaymentApi.Object);

        dispatcher.DispatchOrder("Burger", 5.00m, "C002");

        // Verify that the interaction occurred
        // Mocks act as Spies to verify side-effects (Interactions)
        mockPaymentApi.Verify(x => x.ChargeCard(It.IsAny<string>(), It.IsAny<decimal>()), Times.Once);
    }

    #endregion

    #region THE FAKE (State Verification)

    /// <summary>
    /// Demonstrates the use of Fake test doubles for state verification.
    /// 
    /// FAKE: A lightweight working implementation that maintains internal state.
    /// - Used here: FakePaymentApiClient is a hand-written implementation
    /// - Fakes verify final outcome/state, not just interactions
    /// - No Moq framework used for the fake itself
    /// - Ideal for testing complex state-based logic
    /// 
    /// EXPECTED TO FAIL: This test exposes a bug in the OrderDispatcher where
    /// the return value of ChargeCard is ignored, causing the dispatcher to
    /// always return true even when payment fails.
    /// </summary>
    [Fact]
    public void DispatchOrder_WhenPaymentApiFails_ShouldReturnFalse()
    {
        // [EXPECTED TO FAIL]
        var dummyLogger = new Mock<ILogger>().Object;
        var stubInventory = new Mock<IInventoryService>();
        stubInventory.Setup(x => x.IsInStock(It.IsAny<string>())).Returns(true);

        // Fake: Instantiate FakePaymentApiClient (No Moq).
        var fakeApi = new FakePaymentApiClient();
        fakeApi.SimulateApiFailure = true;

        var dispatcher = new OrderDispatcher(dummyLogger, stubInventory.Object, fakeApi);

        // Call DispatchOrder.
        var result = dispatcher.DispatchOrder("Sushi", 20.00m, "C003");

        // Assert dispatcher returns false.
        // Add a comment explaining the test fails because the SUT ignored the API response. Fakes verify final outcome/state.
        Assert.False(result, "This test fails because the SUT ignored the API response. Fakes verify final outcome/state, exposing bugs that interaction verification might miss.");
    }

    #endregion
}
