using System;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

namespace FoodFast.ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        var engine = new DeliveryPricingEngine();

        Console.WriteLine("========================================");
        Console.WriteLine("   FoodFast Delivery Pricing Engine");
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
}
