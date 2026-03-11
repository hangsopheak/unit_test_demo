using Microsoft.EntityFrameworkCore;

namespace FoodFast.Api.Data;

/// <summary>
/// EF Core DbContext — the bridge between C# objects and SQLite tables.
/// This is the Code-to-Database integration boundary we test.
/// </summary>
public class FoodFastDbContext : DbContext
{
    public FoodFastDbContext(DbContextOptions<FoodFastDbContext> options) : base(options) { }

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
}
