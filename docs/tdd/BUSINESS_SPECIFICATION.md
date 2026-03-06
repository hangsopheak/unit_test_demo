# Discount Calculator - Business Specification

## Overview

The **Discount Calculator** applies promotional discounts to customer orders in the FoodFast platform. It handles percentage and fixed-amount discounts, enforces minimum order thresholds, controls discount stacking, and prevents negative totals.

## Data Model

### DiscountRule

| Property           | Type         | Description                                   |
| ------------------ | ------------ | --------------------------------------------- |
| Id                 | string       | Unique identifier (e.g., "PERC10", "FIX5")    |
| Name               | string       | Display name (e.g., "10% Off Orders $50+")    |
| Type               | DiscountType | Percentage or FixedAmount                     |
| Value              | decimal      | Discount value (10 for 10%, or 5.00 for $5)   |
| MinimumOrderAmount | decimal      | Minimum order required to qualify             |
| CanStack           | bool         | Whether this discount can combine with others |

### DiscountType

| Value       | Description                        |
| ----------- | ---------------------------------- |
| Percentage  | Percentage-based (e.g., 10% off)   |
| FixedAmount | Fixed dollar amount (e.g., $5 off) |

### DiscountResult

| Property       | Type         | Description                         |
| -------------- | ------------ | ----------------------------------- |
| Rule           | DiscountRule | The discount rule applied           |
| DiscountAmount | decimal      | Amount discounted from the order    |
| NewTotal       | decimal      | Order total after applying discount |

## Business Rules

### 1. Discount Calculations

- **Percentage**: `DiscountAmount = OrderTotal × (Value / 100)`  
  Example: 10% off $100 = $10 discount → $90 total
- **Fixed Amount**: `DiscountAmount = Value`  
  Example: $5 off $30 = $25 total

### 2. Minimum Order Requirement

- Discount only applies if `OrderTotal >= MinimumOrderAmount`
- Examples: $30 order with $50 minimum → no discount; $50 order → discount applied

### 3. Stacking Behavior

- **Stackable**: Multiple discounts apply sequentially to the reduced total
  - Order $100 → 10% off → $90 → $5 off → $85 final
- **Non-Stackable**: First non-stackable discount stops further processing
  - Order $100 → 10% off (non-stackable) → $90 final (next discount ignored)

### 4. Negative Total Protection

- Order total cannot become negative: `NewTotal = max(0, OrderTotal - DiscountAmount)`
- Example: $10 order with $15 discount → capped at $0

## Calculation Flow

1. Initialize empty result list
2. Process each rule in order:
   - Check if order meets minimum amount
   - Calculate discount based on type
   - Apply discount to current total
   - Cap total at zero if negative
   - Record discount application
   - Stop if discount is non-stackable
3. Return all applied discounts

## Example Scenarios

| Scenario                  | Input                                     | Expected Output                                  | Key Rule Tested          |
| ------------------------- | ----------------------------------------- | ------------------------------------------------ | ------------------------ |
| No Discounts              | Order: $40, Rules: None                   | Final: $40                                       | Baseline                 |
| Percentage Discount       | Order: $100, 10% off $50+                 | Discount: $10, Final: $90                        | Percentage calculation   |
| Fixed Amount Discount     | Order: $30, $5 off first order            | Discount: $5, Final: $25                         | Fixed amount calculation |
| Below Minimum Threshold   | Order: $49.99, 10% off $50+               | Final: $49.99 (no discount)                      | Minimum order validation |
| Stacking Discounts        | Order: $100, 10% off + $5 off             | Discount 1: $10, Discount 2: $5, Final: $85      | Stackable behavior       |
| Non-Stackable Discount    | Order: $100, 10% off (non-stack) + $5 off | Discount 1: $10, Discount 2: Ignored, Final: $90 | Non-stackable behavior   |
| Negative Total Protection | Order: $10, $15 off                       | Discount: $15, Final: $0 (capped)                | Negative total cap       |
