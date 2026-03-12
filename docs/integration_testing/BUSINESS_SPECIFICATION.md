# FoodFast API — Business Specification

## Overview

The **FoodFast API** exposes the existing delivery pricing domain as a set of HTTP endpoints backed by a SQLite database. It allows clients to create delivery orders, retrieve them, calculate delivery fees, and delete orders. This is the first time FoodFast has real integration boundaries — network (HTTP), persistence (database), and serialization (JSON).

## Data Model

### OrderEntity (Database)

| Property     | Type     | Description                                  |
|-------------|----------|----------------------------------------------|
| Id          | int      | Auto-increment primary key                   |
| CartSubtotal| decimal  | Subtotal of items in the cart                |
| DistanceInKm| double   | Delivery distance in kilometers              |
| IsRushHour  | bool     | Whether the order is during rush hour        |
| CreatedAt   | DateTime | UTC timestamp when the order was created     |

### CreateOrderRequest (Input DTO)

| Property     | Type    | Description                           |
|-------------|---------|---------------------------------------|
| CartSubtotal| decimal | Cart subtotal (must be >= 0)          |
| DistanceInKm| double  | Delivery distance (0–100 km)          |
| IsRushHour  | bool    | Rush hour flag                        |

### OrderResponse (Output DTO)

| Property     | Type     | Description                                  |
|-------------|----------|----------------------------------------------|
| Id          | int      | Database-generated order ID                  |
| CartSubtotal| decimal  | Cart subtotal as submitted                   |
| DistanceInKm| double   | Distance as submitted                        |
| IsRushHour  | bool     | Rush hour flag as submitted                  |
| DeliveryFee | decimal  | Calculated fee (from DeliveryPricingEngine)  |
| CreatedAt   | DateTime | UTC creation timestamp                       |

### FeeBreakdownResponse (Calculate Fee Endpoint)

| Property     | Type    | Description                                |
|-------------|---------|-------------------------------------------|
| OrderId     | int     | The order ID                              |
| CartSubtotal| decimal | Cart subtotal                             |
| DistanceInKm| double  | Delivery distance                         |
| IsRushHour  | bool    | Rush hour flag                            |
| DeliveryFee | decimal | Calculated delivery fee                   |
| Total       | decimal | CartSubtotal + DeliveryFee                |

## API Contract (Endpoints)

### 1. Create Order — `POST /api/orders`

- **Request Body**: `CreateOrderRequest` as JSON
- **Success**: `201 Created` + `Location: /api/orders/{id}` header + `OrderResponse` body
- **Business Rule**: DeliveryFee is calculated automatically on creation

### 2. Get Order — `GET /api/orders/{id}`

- **Success**: `200 OK` + `OrderResponse` body
- **Not Found**: `404 Not Found` + `{ error, orderId }` body

### 3. List Orders — `GET /api/orders`

- **Success**: `200 OK` + array of `OrderResponse` (may be empty)
- **Ordering**: Most recent first (descending by CreatedAt)

### 4. Delete Order — `DELETE /api/orders/{id}`

- **Success**: `204 No Content` (no body)
- **Not Found**: `404 Not Found` + `{ error, orderId }` body

### 5. Calculate Fee — `POST /api/orders/{id}/calculate-fee`

- **Success**: `200 OK` + `FeeBreakdownResponse` body
- **Not Found**: `404 Not Found` + `{ error, orderId }` body
- **Business Rule**: `Total = CartSubtotal + DeliveryFee`

## Validation Rules

The `POST /api/orders` endpoint validates input before creating an order:

| Rule | Condition | Error Message |
|---|---|---|
| Cart subtotal non-negative | `CartSubtotal < 0` | "CartSubtotal must be >= 0." |
| Distance positive | `DistanceInKm <= 0` | "DistanceInKm must be > 0." |
| Distance within range | `DistanceInKm > 100` | "DistanceInKm must be <= 100." |

- **Response:** `400 Bad Request` with `{ "errors": ["..."] }`
- **Multiple errors:** All validation errors are returned at once (not short-circuited)
- **Boundary alignment:** These rules match the `DeliveryPricingEngine` constraints from sessions 1–4

## Error Response Formats

All error responses use JSON. The API produces two distinct shapes:

### 400 Bad Request (Validation Error)

Returned by `POST /api/orders` when input fails validation.

```json
{
  "errors": [
    "CartSubtotal must be >= 0.",
    "DistanceInKm must be > 0."
  ]
}
```

| Property | Type       | Description                              |
|----------|------------|------------------------------------------|
| errors   | string[]   | Array of all validation error messages   |

- The `errors` array may contain 1–3 messages (one per failed rule)
- All rules are evaluated — errors are not short-circuited

### 404 Not Found

Returned by `GET /api/orders/{id}`, `DELETE /api/orders/{id}`, and `POST /api/orders/{id}/calculate-fee` when the order does not exist.

```json
{
  "error": "Order not found",
  "orderId": 999
}
```

| Property | Type   | Description                              |
|----------|--------|------------------------------------------|
| error    | string | Fixed message: `"Order not found"`       |
| orderId  | int    | The requested order ID that was not found|

### 500 Internal Server Error

No custom error handling — ASP.NET Core returns its default 500 response. This is intentional for the demo; production APIs would add global error middleware.

## Business Rules (Delivery Fee)

These rules are inherited from `DeliveryPricingEngine` (sessions 1–4):

1. **Distance tiers**: < 5 km = $2.00, 5–10 km = $5.00, >= 10 km = $10.00
2. **Rush hour surcharge**: Fee × 1.5 during rush hour
3. **Free delivery**: $0 fee when CartSubtotal >= $50.00
4. **Validation**: Distance must be 0–100 km, order cannot be null

## Calculation Flow (End-to-End)

1. Client sends `POST /api/orders` with JSON body
2. API deserializes JSON into `CreateOrderRequest`
3. API creates `OrderEntity` and persists to SQLite
4. API maps entity to `DeliveryOrder` domain model
5. `DeliveryPricingEngine.CalculateFee()` calculates the fee
6. API returns `201 Created` with `OrderResponse` including the fee
7. Client can later `GET /api/orders/{id}` to retrieve the same data

## Example Scenarios

| Scenario | Input | Expected Response | Key Boundary Tested |
|---|---|---|---|
| Create standard order | POST `{ cartSubtotal: 30, distanceInKm: 6, isRushHour: false }` | 201, fee: $5.00 | JSON serialization + DB persistence |
| Create free delivery order | POST `{ cartSubtotal: 60, distanceInKm: 3, isRushHour: true }` | 201, fee: $0.00 | Free delivery threshold through HTTP |
| Get existing order | GET `/api/orders/1` | 200, body matches creation | DB read + JSON serialization |
| Get non-existent order | GET `/api/orders/999` | 404, `{ error: "Order not found" }` | Error contract enforcement |
| Calculate fee breakdown | POST `/api/orders/1/calculate-fee` | 200, total = subtotal + fee | Business logic through network |
| Delete order then GET | DELETE then GET `/api/orders/1` | 204, then 404 | State change + contract enforcement |
| Rush hour order | POST `{ cartSubtotal: 20, distanceInKm: 3, isRushHour: true }` | 201, fee: $3.00 (2 × 1.5) | Rush hour surcharge through full stack |
| Negative cart subtotal | POST `{ cartSubtotal: -10, distanceInKm: 5, isRushHour: false }` | 400, `{ errors: ["CartSubtotal must be >= 0."] }` | Validation rejects bad input |
| Multiple validation errors | POST `{ cartSubtotal: -5, distanceInKm: 200, isRushHour: false }` | 400, `{ errors: ["CartSubtotal must be >= 0.", "DistanceInKm must be <= 100."] }` | All errors returned at once (not short-circuited) |
