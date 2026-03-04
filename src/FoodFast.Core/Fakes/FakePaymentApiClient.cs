using FoodFast.Core.Interfaces;

namespace FoodFast.Core.Fakes;

/// <summary>
/// PHASE 2: THE FAKE - Manual Test Double Implementation
/// 
/// A Fake is a lightweight, working implementation of an interface that is suitable for testing.
/// Unlike mocks (which are created with frameworks like Moq), Fakes are hand-written classes
/// that maintain internal state and provide realistic behavior without external dependencies.
/// 
/// Key characteristics:
/// - No external dependencies (no network calls, no databases)
/// - Maintains internal state for verification
/// - Configurable behavior (e.g., simulate failures)
/// - Ideal for state verification (checking final outcomes)
/// </summary>
public class FakePaymentApiClient : IExternalPaymentApi
{
    // State: Track all processed charges for verification
    public List<decimal> ProcessedCharges { get; } = new();

    // Toggle: Simulate API failure scenarios
    public bool SimulateApiFailure { get; set; } = false;

    public bool ChargeCard(string customerId, decimal amount)
    {
        // Logic: If SimulateApiFailure is true, return false.
        if (SimulateApiFailure)
        {
            return false;
        }

        // Logic: Add amount to ProcessedCharges and return true.
        ProcessedCharges.Add(amount);
        return true;
    }
}
