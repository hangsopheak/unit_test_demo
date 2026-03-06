# TDD Implementation Plan - Discount Calculator

## Overview

This is a **collaborative planning worksheet** for building the DiscountCalculator feature using Test-Driven Development (TDD). Work through this document together with your lecturer to plan and implement each feature step-by-step using the Red-Green-Refactor cycle.

## How to Use This Document

1. **Read each section** before starting implementation
2. **Discuss with your lecturer** the approach and design decisions
3. **Fill in the planning sections** with your own ideas
4. **Implement together** following the TDD cycle
5. **Reflect on what you learned** after each cycle

## Part 1: Implementation Evaluation & Student Handout

### The "Why": Architectural Scenario

The **DiscountCalculator** is a business-critical component that determines promotional discounts for food delivery orders. It must:

- Apply percentage-based discounts (e.g., "10% off orders over $50")
- Apply fixed-amount discounts (e.g., "$5 off for first-time customers")
- Handle multiple discount combinations with stacking rules
- Validate discount eligibility based on order criteria
- Ensure discounts never make the order total negative

**Why TDD matters here:** Discount logic is notoriously error-prone. Business rules change frequently, edge cases abound (negative totals, invalid combinations), and bugs directly impact revenue. TDD ensures every rule is explicitly tested before implementation.

### Concept Mapping to Lecture Slides

| Lecture Concept              | Implementation Demonstration                                                                                                            |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| **Red-Green-Refactor Cycle** | Each discount rule is implemented by first writing a failing test (Red), then minimal code to pass (Green), then cleaning up (Refactor) |
| **Test-First Development**   | No production code is written until a test exists that requires it                                                                      |
| **Design for Testability**   | The calculator uses pure functions with clear inputs/outputs, making it inherently testable                                             |
| **Unit Test Isolation**      | Each test focuses on a single discount rule without external dependencies                                                               |
| **AAA Pattern**              | All tests follow Arrange-Act-Assert structure                                                                                           |
| **Test Readability**         | Test names clearly describe the business rule being validated                                                                           |
| **Refactoring Confidence**   | Existing tests serve as a safety net when improving code structure                                                                      |

### Adaptability: Core Principles for Student Projects

The TDD approach demonstrated here can be adapted to any domain:

1. **Start with the simplest case**: Write a test for the most basic scenario first (e.g., "no discount applied")
2. **One test at a time**: Don't write multiple tests before implementing. Let each test drive the next piece of functionality
3. **Fake it 'til you make it**: Return hardcoded values initially, then generalize
4. **Refactor continuously**: Once tests pass, improve the code structure without changing behavior
5. **Test names as documentation**: Descriptive test names serve as living documentation of business rules

**Applicable to any project:**

- E-commerce: Price calculators, shipping rules, tax computations
- Finance: Interest calculations, loan eligibility, risk scoring
- Healthcare: Treatment eligibility, dosage calculations, insurance claims
- Education: Grade calculations, attendance policies, scholarship eligibility

---

## Part 2: Step-by-Step Deconstruction (The "How")

### Phase 1: Setup & Scaffolding

#### Step 1: Define the Domain Model

**File**: [`src/FoodFast.Core/Models/DiscountRule.cs`](../src/FoodFast.Core/Models/DiscountRule.cs)

```csharp
namespace FoodFast.Core.Models;

/// <summary>
/// Represents a discount rule that can be applied to an order.
///
/// ARCHITECTURAL CHOICE: Using a simple model class with clear properties.
/// This makes the domain model explicit and easy to test.
///
/// REAL-WORLD BEST PRACTICE: Domain models should be pure data structures
/// without business logic. Logic lives in services that operate on these models.
/// </summary>
public class DiscountRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public bool CanStack { get; set; }
}

public enum DiscountType
{
    Percentage,
    FixedAmount
}
```

**File**: [`src/FoodFast.Core/Models/DiscountResult.cs`](../src/FoodFast.Core/Models/DiscountResult.cs)

```csharp
namespace FoodFast.Core.Models;

/// <summary>
/// Represents the result of applying a discount to an order.
///
/// ARCHITECTURAL CHOICE: Separate result object allows us to track
/// which discounts were applied and why, which is valuable for:
/// - Customer-facing receipts
/// - Analytics and reporting
/// - Debugging discount calculations
///
/// REAL-WORLD BEST PRACTICE: Rich result objects provide more context
/// than simple return values, making the system easier to understand
/// and debug.
/// </summary>
public class DiscountResult
{
    public DiscountRule Rule { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
    public decimal NewTotal { get; set; }
}
```

#### Step 2: Create the Service Interface

**File**: [`src/FoodFast.Core/Interfaces/IDiscountCalculator.cs`](../src/FoodFast.Core/Interfaces/IDiscountCalculator.cs)

```csharp
using FoodFast.Core.Models;

namespace FoodFast.Core.Interfaces;

/// <summary>
/// Defines the contract for calculating discounts on orders.
///
/// ARCHITECTURAL CHOICE: Interface-based design enables:
/// - Dependency injection for testability
/// - Multiple implementations (e.g., different discount strategies)
/// - Easy mocking in unit tests
///
/// REAL-WORLD BEST PRACTICE: Interfaces should be focused and follow
/// the Interface Segregation Principle. This interface has a single
/// responsibility: calculating discounts.
/// </summary>
public interface IDiscountCalculator
{
    List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules);
}
```

---

### Phase 2: Core Logic - TDD Implementation (45 minutes)

#### TDD Cycle 1: No Discounts Applied

**Test First** - [`tests/FoodFast.Tests/DiscountCalculatorTests.cs`](../tests/FoodFast.Tests/DiscountCalculatorTests.cs)

```csharp
[Fact]
public void CalculateDiscounts_WhenNoRulesApply_ReturnsEmptyList()
{
    // Arrange
    decimal orderTotal = 50.00m;
    var applicableRules = new List<DiscountRule>();

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Empty(result);
}
```

**Implementation** - Create [`DiscountCalculator.cs`](../src/FoodFast.Core/Services/DiscountCalculator.cs)

```csharp
public class DiscountCalculator : IDiscountCalculator
{
    public List<DiscountResult> CalculateDiscounts(
        decimal orderTotal,
        List<DiscountRule> applicableRules)
    {
        return new List<DiscountResult>();
    }
}
```

**Run Test**: ✅ PASSES

**TDD Lesson**: Start with the simplest case to establish baseline behavior.

---

#### TDD Cycle 2: Percentage Discount

**Test First**:

```csharp
[Fact]
public void CalculateDiscounts_WithPercentageDiscount_AppliesCorrectAmount()
{
    // Arrange
    decimal orderTotal = 100.00m;
    var rule = new DiscountRule
    {
        Id = "PERC10",
        Name = "10% Off",
        Type = DiscountType.Percentage,
        Value = 10m,
        MinimumOrderAmount = 50.00m,
        CanStack = false
    };
    var applicableRules = new List<DiscountRule> { rule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Single(result);
    Assert.Equal(rule, result[0].Rule);
    Assert.Equal(10.00m, result[0].DiscountAmount); // 10% of $100
    Assert.Equal(90.00m, result[0].NewTotal); // $100 - $10
}
```

**Implementation**:

```csharp
public List<DiscountResult> CalculateDiscounts(
    decimal orderTotal,
    List<DiscountRule> applicableRules)
{
    var applications = new List<DiscountResult>();

    foreach (var rule in applicableRules)
    {
        decimal discountAmount = rule.Type switch
        {
            DiscountType.Percentage => orderTotal * (rule.Value / 100m),
            _ => 0m
        };

        applications.Add(new DiscountResult
        {
            Rule = rule,
            DiscountAmount = discountAmount,
            NewTotal = orderTotal - discountAmount
        });
    }

    return applications;
}
```

**Run Test**: ✅ PASSES

**Refactor**: Extract `CalculateDiscountAmount` method for clarity.

**TDD Lesson**: Add one feature at a time. Tests guide the implementation naturally.

---

#### TDD Cycle 3: Fixed Amount Discount (5 minutes)

**Test First**:

```csharp
[Fact]
public void CalculateDiscounts_WithFixedAmountDiscount_AppliesCorrectAmount()
{
    // Arrange
    decimal orderTotal = 30.00m;
    var rule = new DiscountRule
    {
        Id = "FIX5",
        Name = "$5 Off First Order",
        Type = DiscountType.FixedAmount,
        Value = 5.00m,
        MinimumOrderAmount = 0.00m,
        CanStack = false
    };
    var applicableRules = new List<DiscountRule> { rule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Single(result);
    Assert.Equal(rule, result[0].Rule);
    Assert.Equal(5.00m, result[0].DiscountAmount);
    Assert.Equal(25.00m, result[0].NewTotal);
}
```

**Implementation**: Add fixed amount case to switch statement.

```csharp
decimal discountAmount = rule.Type switch
{
    DiscountType.Percentage => orderTotal * (rule.Value / 100m),
    DiscountType.FixedAmount => rule.Value,
    _ => throw new ArgumentException($"Unknown discount type: {rule.Type}")
};
```

**Run Test**: ✅ PASSES

**TDD Lesson**: Test one discount type at a time before combining them.

---

#### TDD Cycle 4: Minimum Order Amount

**Test First**:

```csharp
[Fact]
public void CalculateDiscounts_WhenOrderBelowMinimum_DoesNotApplyDiscount()
{
    // Arrange
    decimal orderTotal = 30.00m;
    var rule = new DiscountRule
    {
        Id = "PERC10",
        Name = "10% Off",
        Type = DiscountType.Percentage,
        Value = 10m,
        MinimumOrderAmount = 50.00m,
        CanStack = false
    };
    var applicableRules = new List<DiscountRule> { rule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Empty(result);
}
```

**Implementation**: Add minimum amount check.

```csharp
foreach (var rule in applicableRules)
{
    if (orderTotal < rule.MinimumOrderAmount)
        continue;

    decimal discountAmount = CalculateDiscountAmount(orderTotal, rule);
    // ... rest of implementation
}
```

**Run Test**: ✅ PASSES

**TDD Lesson**: Test minimum order validation to ensure discounts only apply when eligible.

---

#### TDD Cycle 5: Stacking Discounts

**Test First - Stackable**:

```csharp
[Fact]
public void CalculateDiscounts_WithStackableDiscounts_AppliesAll()
{
    // Arrange
    decimal orderTotal = 100.00m;
    var percentageRule = new DiscountRule
    {
        Id = "PERC10",
        Name = "10% Off",
        Type = DiscountType.Percentage,
        Value = 10m,
        MinimumOrderAmount = 50.00m,
        CanStack = true
    };
    var fixedRule = new DiscountRule
    {
        Id = "FIX5",
        Name = "$5 Off",
        Type = DiscountType.FixedAmount,
        Value = 5.00m,
        MinimumOrderAmount = 0.00m,
        CanStack = true
    };
    var applicableRules = new List<DiscountRule> { percentageRule, fixedRule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Equal(2, result.Count);

    // First discount: 10% of $100 = $10 off
    Assert.Equal(percentageRule, result[0].Rule);
    Assert.Equal(10.00m, result[0].DiscountAmount);
    Assert.Equal(90.00m, result[0].NewTotal);

    // Second discount: $5 off (applied to $90)
    Assert.Equal(fixedRule, result[1].Rule);
    Assert.Equal(5.00m, result[1].DiscountAmount);
    Assert.Equal(85.00m, result[1].NewTotal);
}
```

**Test First - Non-Stackable**:

```csharp
[Fact]
public void CalculateDiscounts_WithNonStackableDiscount_OnlyAppliesFirst()
{
    // Arrange
    decimal orderTotal = 100.00m;
    var firstRule = new DiscountRule
    {
        Id = "PERC10",
        Name = "10% Off",
        Type = DiscountType.Percentage,
        Value = 10m,
        MinimumOrderAmount = 50.00m,
        CanStack = false
    };
    var secondRule = new DiscountRule
    {
        Id = "FIX5",
        Name = "$5 Off",
        Type = DiscountType.FixedAmount,
        Value = 5.00m,
        MinimumOrderAmount = 0.00m,
        CanStack = true
    };
    var applicableRules = new List<DiscountRule> { firstRule, secondRule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Single(result);
    Assert.Equal(firstRule, result[0].Rule);
    Assert.Equal(10.00m, result[0].DiscountAmount);
}
```

**Implementation**: Add stacking logic with running total.

```csharp
public List<DiscountResult> CalculateDiscounts(
    decimal orderTotal,
    List<DiscountRule> applicableRules)
{
    var applications = new List<DiscountResult>();
    decimal currentTotal = orderTotal;

    foreach (var rule in applicableRules)
    {
        if (currentTotal < rule.MinimumOrderAmount)
            continue;

        decimal discountAmount = CalculateDiscountAmount(currentTotal, rule);
        currentTotal -= discountAmount;

        applications.Add(new DiscountResult
        {
            Rule = rule,
            DiscountAmount = discountAmount,
            NewTotal = currentTotal
        });

        if (!rule.CanStack)
            break;
    }

    return applications;
}
```

**Run Tests**: ✅ ALL PASS

**TDD Lesson**: Test combinations after individual features work. Ensure business rules interact correctly.

---

#### TDD Cycle 6: Negative Total Protection

**Test First**:

```csharp
[Fact]
public void CalculateDiscounts_WhenDiscountExceedsOrderTotal_CapsAtZero()
{
    // Arrange
    decimal orderTotal = 10.00m;
    var rule = new DiscountRule
    {
        Id = "FIX15",
        Name = "$15 Off",
        Type = DiscountType.FixedAmount,
        Value = 15.00m,
        MinimumOrderAmount = 0.00m,
        CanStack = false
    };
    var applicableRules = new List<DiscountRule> { rule };

    // Act
    var result = _sut.CalculateDiscounts(orderTotal, applicableRules);

    // Assert
    Assert.Single(result);
    Assert.Equal(rule, result[0].Rule);
    Assert.Equal(15.00m, result[0].DiscountAmount);
    Assert.Equal(0.00m, result[0].NewTotal); // Capped at zero
}
```

**Implementation**: Add negative total protection.

```csharp
decimal discountAmount = CalculateDiscountAmount(currentTotal, rule);
currentTotal -= discountAmount;

if (currentTotal < 0)
    currentTotal = 0;
```

**Run Test**: ✅ PASSES

**TDD Lesson**: Test edge cases and error conditions to prevent business logic errors.

---

### Phase 3: Wiring & Testing

#### Integration with Console Application

**File**: [`src/FoodFast.ConsoleApp/Program.cs`](../src/FoodFast.ConsoleApp/Program.cs)

Add menu system and DiscountCalculator demo section:

```csharp
static void RunDiscountCalculatorDemo()
{
    var discountCalculator = new DiscountCalculator();

    Console.WriteLine("========================================");
    Console.WriteLine("   Discount Calculator Demo (TDD)");
    Console.WriteLine("========================================");

    while (true)
    {
        try
        {
            Console.Write("Enter order total ($): ");
            decimal orderTotal = decimal.Parse(Console.ReadLine()!);

            var rules = new List<DiscountRule>
            {
                new DiscountRule
                {
                    Id = "PERC10",
                    Name = "10% Off (Orders $50+)",
                    Type = DiscountType.Percentage,
                    Value = 10m,
                    MinimumOrderAmount = 50.00m,
                    CanStack = true
                },
                new DiscountRule
                {
                    Id = "FIX5",
                    Name = "$5 Off (First Order)",
                    Type = DiscountType.FixedAmount,
                    Value = 5.00m,
                    MinimumOrderAmount = 0.00m,
                    CanStack = true
                }
            };

            var discounts = discountCalculator.CalculateDiscounts(orderTotal, rules);

            // Display results...
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

#### Running the Tests

```bash
# Run all discount calculator tests
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~DiscountCalculator"

# Run with detailed output
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~DiscountCalculator" --logger "console;verbosity=detailed"

# Run specific test
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~CalculateDiscounts_WithPercentageDiscount_AppliesCorrectAmount"
```

---

## Part 3: Lecturer's Demo Flow

### Action Sequence

#### Introduction

1. **Explain TDD**: Brief overview of Red-Green-Refactor cycle
2. **Set context**: We're building a DiscountCalculator from scratch
3. **Show goal**: Demonstrate how tests drive design and catch bugs early

#### TDD Cycle 1: No Discounts

1. **Ask students**: "What should happen when there are no discount rules?"
2. **Write test**: `CalculateDiscounts_WhenNoRulesApply_ReturnsEmptyList()`
3. **Run test**: ❌ FAILS (class doesn't exist)
4. **Create class**: Add `DiscountCalculator` class with empty method
5. **Run test**: ❌ FAILS (returns null instead of empty list)
6. **Fix implementation**: Return `new List<DiscountResult>()`
7. **Run test**: ✅ PASSES
8. **Explain**: This is the RED-GREEN cycle in action

#### TDD Cycle 2: Percentage Discount

1. **Ask students**: "What's the simplest discount rule we can implement?"
2. **Write test**: `CalculateDiscounts_WithPercentageDiscount_AppliesCorrectAmount()`
3. **Run test**: ❌ FAILS (no implementation)
4. **Add model**: Create `DiscountRule` and `DiscountResult` classes
5. **Implement**: Add percentage calculation logic
6. **Run test**: ✅ PASSES
7. **Refactor**: Extract `CalculateDiscountAmount` method
8. **Run tests**: ✅ ALL PASS (refactoring didn't break anything)

#### TDD Cycle 3: Fixed Amount Discount

1. **Write test**: `CalculateDiscounts_WithFixedAmountDiscount_AppliesCorrectAmount()`
2. **Run test**: ❌ FAILS (doesn't handle fixed amounts)
3. **Implement**: Add switch statement for discount types
4. **Run test**: ✅ PASSES
5. **Explain**: Tests guide the implementation naturally

#### TDD Cycle 4: Minimum Order Amount

1. **Write test**: `CalculateDiscounts_WhenOrderBelowMinimum_DoesNotApplyDiscount()`
2. **Run test**: ❌ FAILS (applies discount regardless of minimum)
3. **Implement**: Add minimum amount check
4. **Run test**: ✅ PASSES
5. **Explain**: Minimum order validation ensures discounts only apply when eligible

#### TDD Cycle 5: Stacking Discounts

1. **Write test**: `CalculateDiscounts_WithStackableDiscounts_AppliesAll()`
2. **Run test**: ❌ FAILS (only applies first discount)
3. **Implement**: Add loop to process all stackable rules
4. **Run test**: ✅ PASSES
5. **Write non-stacking test**: `CalculateDiscounts_WithNonStackableDiscount_OnlyAppliesFirst()`
6. **Run test**: ✅ PASSES
7. **Explain**: Tests ensure business rules are enforced correctly

#### TDD Cycle 6: Negative Total Protection

1. **Write test**: `CalculateDiscounts_WhenDiscountExceedsOrderTotal_CapsAtZero()`
2. **Run test**: ❌ FAILS (allows negative totals)
3. **Implement**: Add cap at zero
4. **Run test**: ✅ PASSES
5. **Explain**: Edge case testing prevents business logic errors

#### Demo & Wrap-up

1. **Run ConsoleApp**: Show interactive discount calculator
2. **Run all tests**: Show 100% pass rate
3. **Key takeaways**:
   - Tests drive design
   - Small, focused tests are easier to understand
   - Refactoring is safe with tests
   - Tests serve as living documentation

---

### Complexity Warnings

#### Warning 1: Stacking Logic (Slow down here!)

**⚠️ CONFUSION ALERT:** Students often struggle with how stacking discounts work.

**Why it's complex:**

- Multiple discounts applied sequentially
- Each discount affects the next calculation
- Order of rules matters
- Stacking vs. non-stacking behavior

**Teaching strategy:**

1. **Start with simple case**: One discount only
2. **Add second discount**: Show how it applies to the reduced total
3. **Visualize the flow**: Draw a diagram showing $100 → $90 (10%) → $85 ($5 off)
4. **Test both scenarios**: Stackable and non-stackable
5. **Emphasize the test**: The test clearly shows expected behavior

**Memory trick for students:**

- "Stacking = Layering discounts on top of each other"
- "Non-stacking = First discount wins, others ignored"

#### Warning 2: Minimum Order Validation (Slow down here!)

**⚠️ CONFUSION ALERT:** Students often confuse the comparison operators for minimum order validation.

**Why it's subtle:**

- `>=` vs `>` is easy to confuse
- Business rules can be ambiguous ("$50+" vs "over $50")
- Order amounts are decimal values with precision considerations

**Teaching strategy:**

1. **Show the business rule**: "Discount applies if order is $50 OR MORE"
2. **Ask students**: "What happens at exactly $50?"
3. **Clarify the requirement**: "$50+" means "$50 or more" so use `>=`
4. **Explain the fix**: Use `orderTotal >= MinimumOrderAmount` not `>`
5. **Emphasize**: Always clarify business requirements before coding

**Key takeaway:**

- Clarify ambiguous business rules before implementation
- Use the correct comparison operator based on requirements
- Test with values below and above the threshold

---

## Key Takeaways

### TDD Benefits Demonstrated

1. **Design emerges from tests**: The API and structure naturally evolved from test requirements
2. **Confidence in refactoring**: Tests ensured code improvements didn't break functionality
3. **Living documentation**: Test names clearly describe business rules
4. **Fast feedback cycle**: Each test provides immediate validation
5. **Reduced debugging**: Tests catch errors before they reach production

### Design for Testability Principles

1. **Pure functions**: No side effects, same input = same output
2. **Explicit dependencies**: All dependencies are injected, not created internally
3. **Single Responsibility**: Each method has one clear purpose
4. **Rich return types**: Result objects provide context, not just values
5. **Small, focused methods**: Easier to test, understand, and maintain

### Adapting to Student Projects

Students can apply this TDD approach to any feature:

1. **Start with the simplest case**: What happens with no input/empty data?
2. **Add one feature at a time**: Don't try to implement everything at once
3. **Write descriptive tests**: Test names should read like requirements
4. **Refactor continuously**: Improve code structure after tests pass
5. **Test edge cases**: Always test error conditions and limits

---

## Running the Demo

### ConsoleApp

```bash
cd src/FoodFast.ConsoleApp
dotnet run
```

### Tests

```bash
# Run all discount calculator tests
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~DiscountCalculator"

# Run with detailed output
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~DiscountCalculator" --logger "console;verbosity=detailed"

# Run specific test
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~CalculateDiscounts_WithPercentageDiscount_AppliesCorrectAmount"
```

---

## Additional Resources

### TDD Reference

- **Red-Green-Refactor**: The core TDD cycle
- **Test-First**: Write tests before implementation
- **Baby Steps**: Small, incremental changes
- **Refactoring**: Improve code without changing behavior

### Testing Techniques

- **AAA Pattern**: Arrange, Act, Assert
- **Descriptive Test Names**: Tests as documentation
- **One Assertion Per Test**: Clear, focused tests
- **Test Isolation**: Independent tests
- **Edge Case Testing**: Test error conditions and limits

### Design Principles

- **Single Responsibility**: One reason to change
- **Dependency Injection**: Inject dependencies, don't create them
- **Pure Functions**: No side effects
- **Explicit Interfaces**: Clear contracts
- **Small Methods**: Focused, testable units

---

## 📝 Reflection & Learning Log

After completing the TDD implementation, reflect on your learning:

### What I Learned About TDD

**Red-Green-Refactor Cycle:**

- What was the hardest part of the cycle?
  - ***
- How did it feel to write failing tests first?
  - ***
- Did refactoring feel safer with tests?
  - ***

**Test-First Development:**

- How did writing tests first change your approach to design?
  - ***
- Did you find it easier or harder than writing code first?
  - ***
- What surprised you about the process?
  - ***

**Design for Testability:**

- What design choices made the code easier to test?
  - ***
- How did pure functions help with testing?
  - ***
- Would you use this approach for your own projects?
  - ***

### Challenges & Solutions

**Challenge 1:**

- What went wrong?
  - ***
- How did you solve it?
  - ***

**Challenge 2:**

- What went wrong?
  - ***
- How did you solve it?
  - ***

### Future Improvements

**What would you do differently next time?**

- ***
- ***

**What questions do you still have?**

- ***
- ***

### Applying TDD to Your Project

**Your project idea:**

- ***

**First test you would write:**

- ***

**TDD cycle you're most excited to try:**

- ***

---

## 🎓 Certificate of Completion

**Student Name:** \***\*\*\*\*\*\*\***\_\_\_\***\*\*\*\*\*\*\***
**Date:** \***\*\*\*\*\*\*\***\_\_\_\***\*\*\*\*\*\*\***

**TDD Cycles Completed:**

- [ ] No Discounts (Baseline)
- [ ] Percentage Discount
- [ ] Fixed Amount Discount
- [ ] Minimum Order Amount
- [ ] Stacking Discounts
- [ ] Negative Total Protection

**Key Takeaways:**

1. ***
2. ***
3. ***

**Ready to apply TDD to your own project?**

- [ ] Yes
- [ ] No
- [ ] Maybe (need more practice)

**Instructor Signature:** \***\*\*\*\*\*\*\***\_\_\_\***\*\*\*\*\*\*\***
