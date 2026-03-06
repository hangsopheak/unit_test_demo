using System;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

namespace FoodFast.ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("   FoodFast Demo System");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("Select a demo to run:");
        Console.WriteLine("  1. Delivery Pricing Engine");
        Console.WriteLine("  2. Discount Calculator (TDD Demo)");
        Console.WriteLine();
        Console.Write("Enter your choice (1-2): ");

        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                RunDeliveryPricingEngineDemo();
                break;
            case "2":
                RunDiscountCalculatorDemo();
                break;
            default:
                Console.WriteLine("Invalid choice. Exiting.");
                break;
        }
    }

    static void RunDeliveryPricingEngineDemo()
    {
        var engine = new DeliveryPricingEngine();

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("   Delivery Pricing Engine Demo");
        Console.WriteLine("========================================");
        Console.WriteLine();

        while (true)
        {
            try
            {
                // Get cart subtotal
                Console.Write("Enter cart subtotal ($): ");
                string? cartInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(cartInput))
                {
                    Console.WriteLine("Error: Cart subtotal cannot be empty.");
                    Console.WriteLine();
                    continue;
                }
                decimal cartSubtotal = decimal.Parse(cartInput);

                // Get distance
                Console.Write("Enter distance (km): ");
                string? distanceInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(distanceInput))
                {
                    Console.WriteLine("Error: Distance cannot be empty.");
                    Console.WriteLine();
                    continue;
                }
                double distance = double.Parse(distanceInput);

                // Get rush hour status
                Console.Write("Is rush hour? (y/n): ");
                bool isRushHour = (Console.ReadLine()?.ToLower() ?? "n") == "y";

                // Create order
                var order = new DeliveryOrder
                {
                    CartSubtotal = cartSubtotal,
                    DistanceInKm = distance,
                    IsRushHour = isRushHour
                };

                // Calculate fee
                decimal fee = engine.CalculateFee(order);

                // Display result
                Console.WriteLine();
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("Order Details:");
                Console.WriteLine($"  Cart Subtotal: ${cartSubtotal:F2}");
                Console.WriteLine($"  Distance: {distance:F1} km");
                Console.WriteLine($"  Rush Hour: {(isRushHour ? "Yes" : "No")}");
                Console.WriteLine($"  Delivery Fee: ${fee:F2}");
                Console.WriteLine("----------------------------------------");
                Console.WriteLine();

                // Ask to continue
                Console.Write("Calculate another order? (y/n): ");
                if ((Console.ReadLine()?.ToLower() ?? "n") != "y")
                {
                    break;
                }
                Console.WriteLine();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
            catch (FormatException)
            {
                Console.WriteLine("Error: Invalid input format. Please enter valid numbers.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("Thank you for using FoodFast!");
        Console.WriteLine("========================================");
    }

    static void RunDiscountCalculatorDemo()
    {
        var discountCalculator = new DiscountCalculator();

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("   Discount Calculator Demo (TDD)");
        Console.WriteLine("========================================");
        Console.WriteLine();

        while (true)
        {
            try
            {
                // Get order total
                Console.Write("Enter order total ($): ");
                string? totalInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(totalInput))
                {
                    Console.WriteLine("Error: Order total cannot be empty.");
                    Console.WriteLine();
                    continue;
                }
                decimal orderTotal = decimal.Parse(totalInput);

                // Create sample discount rules inline for demo
                // This reduces cognitive load by eliminating file switching during live demo
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

                // Calculate discounts
                var discounts = discountCalculator.CalculateDiscounts(orderTotal, rules);

                // Display results
                Console.WriteLine();
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("Order Details:");
                Console.WriteLine($"  Original Total: ${orderTotal:F2}");

                if (discounts.Any())
                {
                    Console.WriteLine($"  Discounts Applied: {discounts.Count}");
                    foreach (var discount in discounts)
                    {
                        Console.WriteLine($"    - {discount.Rule.Name}: -${discount.DiscountAmount:F2}");
                        Console.WriteLine($"      New Total: ${discount.NewTotal:F2}");
                    }
                    Console.WriteLine($"  Final Total: ${discounts.Last().NewTotal:F2}");
                }
                else
                {
                    Console.WriteLine("  No discounts applied");
                    Console.WriteLine($"  Final Total: ${orderTotal:F2}");
                }
                Console.WriteLine("----------------------------------------");
                Console.WriteLine();

                // Ask to continue
                Console.Write("Calculate another discount? (y/n): ");
                if ((Console.ReadLine()?.ToLower() ?? "n") != "y")
                {
                    break;
                }
                Console.WriteLine();
            }
            catch (FormatException)
            {
                Console.WriteLine("Error: Invalid input format. Please enter a valid number.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("Thank you for using FoodFast!");
        Console.WriteLine("========================================");
    }
}
