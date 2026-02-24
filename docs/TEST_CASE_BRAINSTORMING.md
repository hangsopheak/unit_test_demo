# FoodFast Delivery Pricing Engine - Test Case Brainstorming

This document outlines the test cases for the DeliveryPricingEngine, organized by testing technique.

## Testing Techniques Overview

| Technique                           | Purpose                    | Focus                              |
| ----------------------------------- | -------------------------- | ---------------------------------- |
| Happy Path                          | Validates normal operation | Expected conditions work correctly |
| Blackbox - Equivalence Partitioning | Reduces test cases         | One representative per partition   |
| Blackbox - Boundary Value Analysis  | Catches off-by-one errors  | Test at, below, above boundaries   |
| Heuristics - Zero-One-Many          | Edge case coverage         | Zero, one, and many values         |
| Heuristics - Goldilocks             | Validates rejection        | Too small and too big values       |
| Whitebox - Branch Coverage          | Code coverage              | All decision outcomes tested       |
| Whitebox - Path Coverage            | Execution paths            | All possible paths tested          |

---

## Region 1: Happy Path Testing

### Test: Typical Lunch Order

**Purpose**: Demonstrate the system works flawlessly under normal, expected conditions

| Input                 | Expected Output |
| --------------------- | --------------- |
| Distance: 6.0 km      |                 |
| Cart Subtotal: $30.00 | Fee: $5.00      |
| Rush Hour: No         |                 |

**Rationale**: Medium distance (5-10km) = $5.00 base fee, no rush hour surcharge, not eligible for free delivery

---

## Region 2: Blackbox - Specification-Based Testing

### Equivalence Partitioning (Distance Buckets)

**Purpose**: Test one representative value from each distance partition

| Test Case | Distance | Cart   | Rush Hour | Expected Fee | Partition                |
| --------- | -------- | ------ | --------- | ------------ | ------------------------ |
| EP-1      | 3.0 km   | $25.00 | No        | $2.00        | Short (< 5.0 km)         |
| EP-2      | 7.5 km   | $25.00 | No        | $5.00        | Medium (5.0 - < 10.0 km) |
| EP-3      | 15.0 km  | $25.00 | No        | $10.00       | Long (>= 10.0 km)        |

### Boundary Value Analysis (Free Delivery Threshold)

**Purpose**: Test at, below, and above the $50.00 free delivery boundary

| Test Case | Cart Subtotal | Distance | Rush Hour | Expected Fee | Notes                                        |
| --------- | ------------- | -------- | --------- | ------------ | -------------------------------------------- |
| BVA-1     | $49.99        | 6.0 km   | No        | $5.00        | Just below threshold - pays fee              |
| BVA-2     | $50.00        | 6.0 km   | No        | $0.00        | AT threshold - FREE (EXPECTED TO FAIL - BUG) |
| BVA-3     | $50.01        | 6.0 km   | No        | $0.00        | Just above threshold - FREE                  |

**Bug Note**: The implementation uses `>` instead of `>=`, so BVA-2 will fail (returns $5.00 instead of $0.00)

---

## Region 3: Heuristics - Zero, One, Many & Goldilocks

### Zero-One-Many Heuristic

**Purpose**: Validate behavior with zero, one, and many elements

| Test Case | Distance | Cart   | Rush Hour | Expected Fee | Heuristic            |
| --------- | -------- | ------ | --------- | ------------ | -------------------- |
| ZOM-1     | 0.0 km   | $25.00 | No        | $2.00        | Zero (minimum valid) |
| ZOM-2     | 1.0 km   | $25.00 | No        | $2.00        | One (single unit)    |
| ZOM-3     | 25.0 km  | $25.00 | No        | $10.00       | Many (larger value)  |

### Goldilocks Heuristic

**Purpose**: Validate rejection of "too small" and "too big" values

| Test Case | Distance | Expected Exception            | Reason               |
| --------- | -------- | ----------------------------- | -------------------- |
| G-1       | -1.0 km  | `ArgumentOutOfRangeException` | Too Small (negative) |
| G-2       | 101.0 km | `InvalidOperationException`   | Too Big (> 100 km)   |

---

## Region 4: Whitebox - Line, Branch, and Path Coverage

### Branch Coverage (IsRushHour Decision)

**Purpose**: Ensure both true and false outcomes of the rush hour branch are tested

| Test Case | Distance | Cart   | Rush Hour | Expected Fee | Branch                  |
| --------- | -------- | ------ | --------- | ------------ | ----------------------- |
| BC-1      | 6.0 km   | $30.00 | **True**  | $7.50        | IsRushHour = True path  |
| BC-2      | 6.0 km   | $30.00 | **False** | $5.00        | IsRushHour = False path |

**BC-1 Calculation**: $5.00 (base) × 1.5 (rush hour) = $7.50

### Path Coverage (Override Path)

**Purpose**: Test the execution path where free delivery overrides rush hour surcharge

| Test Case | Distance | Cart    | Rush Hour | Expected Fee | Path                               |
| --------- | -------- | ------- | --------- | ------------ | ---------------------------------- |
| PC-1      | 6.0 km   | $100.00 | **True**  | $0.00        | Rush Hour + Free Delivery Override |

**Execution Flow**:

1. Pass validation (distance is valid)
2. Calculate base fee: $5.00 (medium distance)
3. Apply rush hour multiplier: $5.00 × 1.5 = $7.50
4. **Override with free delivery**: Cart > $50.00 → Return $0.00

**Proof**: This test proves that free delivery logic occurs AFTER rush hour calculation and correctly overrides it.

---

## Additional Boundary Value Tests (Distance)

| Test Case | Distance | Expected Fee | Notes                       |
| --------- | -------- | ------------ | --------------------------- |
| DBV-1     | 0.0 km   | $2.00        | Minimum valid distance      |
| DBV-2     | 4.999 km | $2.00        | Just below 5.0 km boundary  |
| DBV-3     | 5.0 km   | $5.00        | At 5.0 km boundary          |
| DBV-4     | 9.999 km | $5.00        | Just below 10.0 km boundary |
| DBV-5     | 10.0 km  | $10.00       | At 10.0 km boundary         |
| DBV-6     | 100.0 km | $10.00       | Maximum valid distance      |

---

## Test Case Summary

| Region    | Technique                | Test Count |
| --------- | ------------------------ | ---------- |
| 1         | Happy Path               | 1          |
| 2         | Equivalence Partitioning | 3          |
| 2         | Boundary Value Analysis  | 3          |
| 3         | Zero-One-Many            | 3          |
| 3         | Goldilocks               | 2          |
| 4         | Branch Coverage          | 2          |
| 4         | Path Coverage            | 1          |
| **Total** |                          | **15**     |

---

## Expected Test Results

| Test                | Expected Status           |
| ------------------- | ------------------------- |
| Happy Path          | ✅ PASS                   |
| EP-1, EP-2, EP-3    | ✅ PASS                   |
| BVA-1, BVA-3        | ✅ PASS                   |
| BVA-2               | ❌ FAIL (intentional bug) |
| ZOM-1, ZOM-2, ZOM-3 | ✅ PASS                   |
| G-1, G-2            | ✅ PASS                   |
| BC-1, BC-2          | ✅ PASS                   |
| PC-1                | ✅ PASS                   |

**Overall**: 14/15 tests pass, 1 test fails (intentionally) to demonstrate boundary value defect detection.
