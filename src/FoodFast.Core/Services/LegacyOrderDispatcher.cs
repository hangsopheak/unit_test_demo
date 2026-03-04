using FoodFast.Core.Interfaces;

namespace FoodFast.Core.Services;

/// <summary>
/// PHASE 1: THE PROBLEM - Non-Testable Design
/// 
/// This class demonstrates BAD design practices that make unit testing difficult or impossible.
/// The key issue is the use of the 'new' keyword inside the method, which creates a tight coupling
/// to a concrete implementation (RealPaymentApi). This prevents us from swapping in test doubles.
/// </summary>
public class LegacyOrderDispatcher
{
    private readonly ILogger _logger;
    private readonly IInventoryService _inventoryService;

    public LegacyOrderDispatcher(ILogger logger, IInventoryService inventoryService)
    {
        _logger = logger;
        _inventoryService = inventoryService;
    }

    public bool DispatchOrder(string item, decimal price, string customerId)
    {
        _logger.LogInfo($"Dispatching order: {item}");

        if (!_inventoryService.IsInStock(item))
        {
            return false;
        }

        // NON-TESTABLE: Because of the 'new' keyword, we cannot swap this for a test double. 
        // This code is welded to the real API. Every test that calls this method will 
        // actually hit the real payment API, making tests slow, unreliable, and expensive.
        var paymentApi = new RealPaymentApi();

        var paymentSuccess = paymentApi.ChargeCard(customerId, price);

        return paymentSuccess;
    }
}

/// <summary>
/// Real implementation of the external payment API.
/// This represents a concrete dependency that would make network calls in production.
/// </summary>
public class RealPaymentApi : IExternalPaymentApi
{
    public bool ChargeCard(string customerId, decimal amount)
    {
        // In a real application, this would make an HTTP request to an external payment gateway
        // For demonstration purposes, we'll simulate a successful charge
        // In production, this would involve:
        // - Network calls to Stripe/PayPal/etc.
        // - API key authentication
        // - Transaction processing
        // - Error handling and retries
        return true;
    }
}
