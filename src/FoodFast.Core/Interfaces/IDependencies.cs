namespace FoodFast.Core.Interfaces;

/// <summary>
/// Interface for logging operations.
/// </summary>
public interface ILogger
{
    void LogInfo(string message);
}

/// <summary>
/// Interface for inventory management operations.
/// </summary>
public interface IInventoryService
{
    bool IsInStock(string itemName);
}

/// <summary>
/// Interface for external payment API operations.
/// </summary>
public interface IExternalPaymentApi
{
    bool ChargeCard(string customerId, decimal amount);
}
