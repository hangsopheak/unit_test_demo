using FoodFast.Core.Interfaces;

namespace FoodFast.Core.Services;

/// <summary>
/// PHASE 2: THE SOLUTION - Testable Design
/// 
/// This class demonstrates GOOD design practices that enable effective unit testing.
/// Key improvements:
/// - Constructor injection: All dependencies are injected, allowing us to swap in test doubles
/// - Interface-based dependencies: Enables mocking and faking
/// - Single Responsibility: Each dependency has a clear purpose
/// </summary>
public class OrderDispatcher
{
    private readonly ILogger _logger;
    private readonly IInventoryService _inventoryService;
    private readonly IExternalPaymentApi _paymentApi;

    public OrderDispatcher(ILogger logger, IInventoryService inventoryService, IExternalPaymentApi paymentApi)
    {
        _logger = logger;
        _inventoryService = inventoryService;
        _paymentApi = paymentApi;
    }

    public bool DispatchOrder(string item, decimal price, string customerId)
    {
        // Step 1: Log the attempt
        _logger.LogInfo($"Dispatching order: {item}");

        // Step 2: Check Inventory. If false, return false.
        if (!_inventoryService.IsInStock(item))
        {
            return false;
        }

        // Step 3: Call _paymentApi.ChargeCard(...)
        // INTENTIONAL BUG: Ignoring the API response to demonstrate why state-verification is sometimes needed over simple interaction verification.
        _paymentApi.ChargeCard(customerId, price);

        // Step 4: Always returns true if the item was in stock (BUG!)
        return true;
    }
}
