namespace FoodFast.Api.Data;

public static class DbSeeder
{
    public static void Seed(FoodFastDbContext db)
    {
        db.Database.EnsureCreated();

        if (db.Orders.Any())
            return;

        var names = new[]
        {
            "Alice", "Bob", "Charlie", "Dave", "Eve", "Frank", "Grace", "Hank", "Ivy", "Jack",
            "Karen", "Leo", "Mia", "Noah", "Olivia", "Paul", "Quinn", "Ruby", "Sam", "Tina"
        };
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            db.Orders.Add(new OrderEntity
            {
                CustomerName = names[i % names.Length],
                CartSubtotal = Math.Round((decimal)(random.NextDouble() * 80 + 5), 2),
                DistanceInKm = Math.Round(random.NextDouble() * 30 + 1, 1),
                IsRushHour   = random.Next(3) == 0,
                CreatedAt    = DateTime.UtcNow.AddMinutes(-(100 - i))
            });
        }
        db.SaveChanges();
    }
}
