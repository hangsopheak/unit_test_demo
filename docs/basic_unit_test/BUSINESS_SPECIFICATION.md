# FoodFast Delivery Pricing Engine - Business Specification

## Overview

Calculates delivery fees based on distance, rush hour status, and cart subtotal.

## Data Model

### DeliveryOrder

| Property     | Type    | Description                                            |
| ------------ | ------- | ------------------------------------------------------ |
| CartSubtotal | decimal | Total value of items in the shopping cart              |
| DistanceInKm | double  | Delivery distance in kilometers                        |
| IsRushHour   | bool    | Indicates whether the order is placed during rush hour |

## Business Rules

### 1. Input Validation

- **Negative Distance**: Throw `ArgumentOutOfRangeException` when distance < 0
- **Excessive Distance**: Throw `InvalidOperationException` when distance > 100 km

### 2. Base Fee Calculation (by Distance)

| Distance Range      | Base Fee |
| ------------------- | -------- |
| < 5.0 km            | $2.00    |
| 5.0 km to < 10.0 km | $5.00    |
| >= 10.0 km          | $10.00   |

### 3. Rush Hour Surcharge

- When `IsRushHour = true`: Multiply base fee by 1.5 (50% surcharge)

### 4. Free Delivery Override

- **Rule**: Free delivery if cart subtotal is **$50.00 OR MORE** (>=)
- **Result**: Return $0.00 delivery fee (overrides rush hour surcharge)

## Calculation Flow

1. Validate input (distance must be 0-100 km)
2. Determine base fee based on distance
3. Apply rush hour surcharge (if applicable)
4. Apply free delivery override (if applicable)
5. Return final fee

## Example Scenarios

### Happy Path Examples

**Example 1: Standard Lunch Order**

- Distance: 6 km
- Cart Subtotal: $30.00
- Rush Hour: No
- **Result**: $5.00 (medium distance, no surcharge)

**Example 2: Free Delivery**

- Distance: 8 km
- Cart Subtotal: $55.00
- Rush Hour: No
- **Result**: $0.00 (cart >= $50.00)

**Example 3: Rush Hour Order**

- Distance: 4 km
- Cart Subtotal: $25.00
- Rush Hour: Yes
- **Result**: $3.00 ($2.00 × 1.5)

**Example 4: Rush Hour with Free Delivery**

- Distance: 12 km
- Cart Subtotal: $100.00
- Rush Hour: Yes
- **Result**: $0.00 (free delivery overrides surcharge)

### Sad Path Examples

**Example 1: Invalid Distance (Negative)**

- Distance: -5.0 km
- **Result**: `ArgumentOutOfRangeException`

**Example 2: Distance Too Far**

- Distance: 150 km
- **Result**: `InvalidOperationException`

**Example 3: Just Missed Free Delivery**

- Distance: 6 km
- Cart Subtotal: $49.99
- Rush Hour: No
- **Result**: $5.00 (pays delivery fee)
