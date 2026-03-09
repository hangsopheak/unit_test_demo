# FoodFast Property-Based Testing — Business Specification

## Overview

This session introduces **Property-Based Testing (PBT)** applied to two FoodFast features: the existing `DeliveryPricingEngine` and a new `OrderBasket` service. Rather than verifying specific input-output pairs, PBT defines universal rules (properties) that must hold for *any* valid input. The `OrderBasket` tracks items added to a customer's order during a session and maintains a running total that must always match the sum of items currently in the basket.

---

## Data Model

### DeliveryOrder (existing)

| Property     | Type    | Description                                        |
|--------------|---------|----------------------------------------------------|
| CartSubtotal | decimal | Total value of items in the cart ($0–$200)         |
| DistanceInKm | double  | Delivery distance in km (0–100)                    |
| IsRushHour   | bool    | Whether the order is placed during rush hour       |

### OrderSerializer (new)

| Method      | Signature                              | Description                                      |
|-------------|----------------------------------------|--------------------------------------------------|
| Serialize   | `string Serialize(DeliveryOrder)`      | Converts order to pipe-delimited string          |
| Deserialize | `DeliveryOrder Deserialize(string)`    | Reconstructs order from pipe-delimited string    |

Format: `"CartSubtotal|DistanceInKm|IsRushHour"` (e.g., `"30.00|6|False"`)

### OrderBasket (new)

| Property | Type           | Description                                       |
|----------|----------------|---------------------------------------------------|
| Total    | decimal        | Cached running total of all items currently in basket |
| Count    | int            | Number of items currently in basket               |

### BasketAction (for stateful testing)

| Action         | Parameter    | Description                              |
|----------------|--------------|------------------------------------------|
| AddItem        | price: decimal | Appends item to basket, increases Total |
| RemoveLastItem | —            | Removes most recently added item, decreases Total |
| Clear          | —            | Empties basket, resets Total to $0       |

---

## Business Rules

### OrderSerializer — Inverse (Round-Trip) Property

#### 1. Round-Trip Integrity
- `Deserialize(Serialize(order))` must produce an order equal to the original
- No field is lost or corrupted during the transformation: `CartSubtotal`, `DistanceInKm`, `IsRushHour` must all match exactly

### DiscountCalculator — Commutativity Property

#### 2. Order Independence for Fixed-Amount Discounts
- Applying two stackable fixed-amount discounts in any order must yield the same final total
- `ApplyDiscounts([A, B]) == ApplyDiscounts([B, A])` when both discounts are `FixedAmount` and `CanStack = true`
- Rationale: `total - A - B == total - B - A` (subtraction is commutative for fixed values)

### DeliveryPricingEngine — Universal Properties

#### 3. Non-Negative Fee
- Delivery fee is always `>= $0.00` for any valid order
- There is no scenario where a customer is owed money on delivery

#### 4. Fee Cap
- Delivery fee never exceeds `$15.00`
- Maximum possible fee: long distance ($10.00) × rush hour surcharge (×1.5) = $15.00

#### 5. Free Delivery Guarantee
- When `CartSubtotal > $50.00`, delivery fee is always `$0.00`
- This holds regardless of distance or rush hour status

#### 6. Determinism (Idempotence)
- The same order always produces the same delivery fee
- `CalculateFee(order) == CalculateFee(order)` — no hidden state

### OrderBasket — Invariant

#### 7. Total Consistency
- After any sequence of `AddItem`, `RemoveLastItem`, and `Clear` operations:
  `basket.Total == sum of all items currently in basket`
- This invariant must hold after **every individual operation**, not only at the end

---

## Calculation Flow

### DeliveryPricingEngine (unchanged — see `docs/basic_unit_test/BUSINESS_SPECIFICATION.md`)

1. Validate distance (0–100 km)
2. Determine base fee by distance tier
3. Apply rush hour surcharge (×1.5 if applicable)
4. Apply free delivery override (return $0 if subtotal > $50)

### OrderBasket

1. `AddItem(price)`: append price to internal list; add price to `Total`
2. `RemoveLastItem()`: read last item from list; remove from list; subtract from `Total`
3. `Clear()`: empty the list; set `Total = 0`

---

## Example Scenarios

### OrderSerializer — Inverse Pattern

| Property Tested | Sample Generated Input | Expected Result | Rule |
|---|---|---|---|
| Round-trip integrity | Any valid `DeliveryOrder` | `Deserialize(Serialize(order))` fields match original | Rule 1 |
| Round-trip integrity | Subtotal = $123.45, Distance = 7.5 km, RushHour = true | Deserialized order identical to original | Rule 1 |

### DiscountCalculator — Commutativity Pattern

| Property Tested | Sample Generated Input | Expected Result | Rule |
|---|---|---|---|
| Order independence | Total = $100, DiscountA = $5, DiscountB = $3 (both fixed, stackable) | Final total $92 regardless of order | Rule 2 |
| Order independence | Total = $50, DiscountA = $1, DiscountB = $4 (both fixed, stackable) | Final total $45 regardless of order | Rule 2 |

### DeliveryPricingEngine Properties

| Property Tested | Sample Generated Input | Expected Result | Rule |
|---|---|---|---|
| Non-negative fee | Any valid order | `fee >= 0` | Rule 3 |
| Fee cap | Distance = 15 km, RushHour = true, Subtotal = $10 | `fee == $15.00` (max) | Rule 4 |
| Fee cap | Distance = 99 km, RushHour = true, Subtotal = $49 | `fee <= $15.00` | Rule 4 |
| Free delivery | Subtotal = $50.01, any distance, any rush hour | `fee == $0.00` | Rule 5 |
| Determinism | Any order called twice | Both results identical | Rule 6 |
| Oracle match | Distance = 7 km, RushHour = false, Subtotal = $30 | Real engine = simple engine ($5.00) | Rules 3–6 |

### OrderBasket Stateful Scenarios

| Action Sequence | Model (List) | Basket Total | Invariant Holds? |
|---|---|---|---|
| `Add($5.00)` | [$5.00] | $5.00 | Yes |
| `Add($5.00)` → `Add($3.00)` | [$5.00, $3.00] | $8.00 | Yes |
| `Add($5.00)` → `RemoveLast` | [] | $0.00 | Yes |
| `Add($5.00)` → `Add($3.00)` → `RemoveLast` | [$5.00] | $5.00 | Yes |
| `Add($5.00)` → `Clear` → `Add($2.00)` | [$2.00] | $2.00 | Yes |
| `RemoveLast` on empty basket | [] | $0.00 | Yes (no-op) |
| **Bug scenario:** `Add($0.50)` → `RemoveLast` (90% removal) | [] | **$0.05 (wrong!)** | **No — bug detected** |
