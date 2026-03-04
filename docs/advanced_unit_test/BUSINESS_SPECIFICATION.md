# Business Specification: Order Dispatching System

## Feature Overview
The **Order Dispatcher** is a core component of the FoodFast platform responsible for coordinating the transition from a "validated cart" to an "active delivery." It ensures that no order is processed unless stock is available and payment is secured.

## Functional Requirements
1. **Inventory Validation**: Before any financial transaction, the system must verify with the Inventory Service that the requested item is in stock.
2. **Payment Processing**: The system must charge the customer's payment method for the exact price of the item.
3. **Audit Logging**: Every dispatch attempt (both successful and failed) must be logged for auditing purposes.
4. **Error Handling**: 
    - If an item is out of stock, the order must be aborted immediately without charging the customer.
    - If the payment API fails or returns a failure status, the dispatch must be marked as failed.

## Component Dependencies
- `IInventoryService`: Remote stock management system.
- `IExternalPaymentApi`: Third-party financial gateway (Stripe/PayPal).
- `ILogger`: Internal observability and logging sink.
