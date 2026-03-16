using FoodFast.Api.Data;
using FoodFast.Core.Models;
using FoodFast.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FoodFast.Tests;

/// <summary>
/// Database Integration Tests — testing the Code ↔ Database boundary.
///
/// STRATEGY: In-memory SQLite (Data Source=:memory:)
/// Each test gets a FRESH database — no shared state, no cleanup needed.
/// This maps to the "Ephemeral Environments" slide: every test gets
/// its own private, disposable universe.
///
/// WHAT WE'RE TESTING:
/// - Does EF Core map our C# properties to SQL columns correctly?
/// - Does data survive the write → read roundtrip?
/// - Does deletion actually remove records?
/// - Does decimal precision survive SQLite's storage? (a real trap!)
///
/// WHAT WE'RE NOT TESTING:
/// - Business logic (that's unit tests)
/// - HTTP serialization (that's Postman)
/// - Network behavior (that's WireMock)
/// </summary>
public class DatabaseIntegrationTests : IDisposable
{
    private readonly FoodFastDbContext _db;

    public DatabaseIntegrationTests()
    {
        // Fresh in-memory SQLite for every test — ephemeral by design
        var options = new DbContextOptionsBuilder<FoodFastDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new FoodFastDbContext(options);
        _db.Database.OpenConnection();  // SQLite in-memory requires open connection
        _db.Database.EnsureCreated();   // Create tables from entity model
    }

    #region 1: CREATE + READ ROUNDTRIP

    /// <summary>
    /// The most fundamental integration test: write data, read it back,
    /// verify every field survived the roundtrip through EF Core and SQLite.
    /// </summary>
    [Fact]
    public async Task CreateOrder_ThenReadBack_AllFieldsMatch()
    {
        // Arrange
        var entity = new OrderEntity
        {
            CustomerName = "Alice",
            CartSubtotal = 30.00m,
            DistanceInKm = 6.0,
            IsRushHour = false,
            CreatedAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act — write to DB
        _db.Orders.Add(entity);
        await _db.SaveChangesAsync();

        // Read back from DB (detach and re-query to force a real DB read)
        _db.ChangeTracker.Clear();
        var loaded = await _db.Orders.FindAsync(entity.Id);

        // Assert — every field survived the roundtrip
        Assert.NotNull(loaded);
        Assert.Equal(entity.Id, loaded.Id);
        Assert.Equal("Alice", loaded.CustomerName);
        Assert.Equal(30.00m, loaded.CartSubtotal);
        Assert.Equal(6.0, loaded.DistanceInKm);
        Assert.False(loaded.IsRushHour);
        Assert.Equal(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc), loaded.CreatedAt);
    }

    /// <summary>
    /// Auto-increment: inserting without specifying Id should generate one.
    /// This verifies the PRIMARY KEY AUTOINCREMENT constraint works.
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithoutId_DatabaseGeneratesAutoIncrementId()
    {
        // Arrange
        var entity = new OrderEntity
        {
            CustomerName = "Bob",
            CartSubtotal = 25.00m,
            DistanceInKm = 3.0,
            IsRushHour = true,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _db.Orders.Add(entity);
        await _db.SaveChangesAsync();

        // Assert — DB generated an ID > 0
        Assert.True(entity.Id > 0);
    }

    #endregion

    #region 2: DECIMAL PRECISION — THE SQLITE TRAP

    /// <summary>
    /// SQLite stores decimal as TEXT, not as a numeric type.
    /// This test verifies that $30.99 doesn't become $30.98999... after roundtrip.
    /// This is a REAL bug that unit tests with Moq would never catch.
    /// </summary>
    [Fact]
    public async Task CreateOrder_WithPreciseDecimal_PrecisionSurvivesRoundtrip()
    {
        // Arrange — a price that could lose precision
        var entity = new OrderEntity
        {
            CustomerName = "Charlie",
            CartSubtotal = 30.99m,
            DistanceInKm = 7.5,
            IsRushHour = false,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _db.Orders.Add(entity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var loaded = await _db.Orders.FindAsync(entity.Id);

        // Assert — exact decimal match, not approximate
        Assert.Equal(30.99m, loaded!.CartSubtotal);
    }

    /// <summary>
    /// Verify the delivery fee calculation still works correctly after
    /// the data goes through the DB roundtrip. This is the full chain:
    /// C# decimal → SQLite TEXT → C# decimal → DeliveryPricingEngine.
    /// </summary>
    [Fact]
    public async Task CreateOrder_ThenCalculateFee_FeeMatchesExpectedValue()
    {
        // Arrange
        var entity = new OrderEntity
        {
            CustomerName = "Dave",
            CartSubtotal = 30.00m,
            DistanceInKm = 6.0,
            IsRushHour = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Orders.Add(entity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Act — read from DB, convert to domain model, calculate fee
        var loaded = await _db.Orders.FindAsync(entity.Id);
        var order = new DeliveryOrder
        {
            CartSubtotal = loaded!.CartSubtotal,
            DistanceInKm = loaded.DistanceInKm,
            IsRushHour = loaded.IsRushHour
        };
        var fee = new DeliveryPricingEngine().CalculateFee(order);

        // Assert — 6km, non-rush = medium distance = $5.00
        Assert.Equal(5.00m, fee);
    }

    #endregion

    #region 3: DELETE + VERIFY REMOVAL

    /// <summary>
    /// Verify that deletion actually removes the record from the database.
    /// After delete + SaveChanges, FindAsync should return null.
    /// </summary>
    [Fact]
    public async Task DeleteOrder_ThenReadBack_ReturnsNull()
    {
        // Arrange — create an order
        var entity = new OrderEntity
        {
            CustomerName = "Eve",
            CartSubtotal = 20.00m,
            DistanceInKm = 2.0,
            IsRushHour = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Orders.Add(entity);
        await _db.SaveChangesAsync();
        var id = entity.Id;

        // Act — delete it
        _db.Orders.Remove(entity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Assert — it's truly gone from the DB
        var loaded = await _db.Orders.FindAsync(id);
        Assert.Null(loaded);
    }

    #endregion

    #region 4: MULTIPLE RECORDS — AUTO-INCREMENT & ORDERING

    /// <summary>
    /// Two inserts should produce different auto-increment IDs.
    /// This verifies concurrent/sequential creates don't collide.
    /// </summary>
    [Fact]
    public async Task CreateTwoOrders_GetDifferentIds()
    {
        // Arrange
        var order1 = new OrderEntity { CustomerName = "Frank", CartSubtotal = 10m, DistanceInKm = 1.0, IsRushHour = false, CreatedAt = DateTime.UtcNow };
        var order2 = new OrderEntity { CustomerName = "Grace", CartSubtotal = 20m, DistanceInKm = 2.0, IsRushHour = true, CreatedAt = DateTime.UtcNow };

        // Act
        _db.Orders.AddRange(order1, order2);
        await _db.SaveChangesAsync();

        // Assert — different IDs, both positive
        Assert.NotEqual(order1.Id, order2.Id);
        Assert.True(order1.Id > 0);
        Assert.True(order2.Id > 0);
    }

    /// <summary>
    /// Verify that LINQ OrderByDescending translates to correct SQL.
    /// The most recent order should appear first.
    /// </summary>
    [Fact]
    public async Task GetAllOrders_OrderedByCreatedAtDescending()
    {
        // Arrange
        var older = new OrderEntity { CustomerName = "Hank", CartSubtotal = 10m, DistanceInKm = 1.0, IsRushHour = false, CreatedAt = new DateTime(2026, 1, 1) };
        var newer = new OrderEntity { CustomerName = "Ivy", CartSubtotal = 20m, DistanceInKm = 2.0, IsRushHour = true, CreatedAt = new DateTime(2026, 3, 10) };
        _db.Orders.AddRange(older, newer);
        await _db.SaveChangesAsync();

        // Act
        var results = await _db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();

        // Assert — newest first
        Assert.Equal(2, results.Count);
        Assert.Equal(newer.Id, results[0].Id);
        Assert.Equal(older.Id, results[1].Id);
    }

    #endregion

    #region 5: IDEMPOTENT RE-RUN — NO STATE LEAKS

    /// <summary>
    /// This test verifies that starting from a fresh DB gives zero orders.
    /// Because each test gets its own in-memory SQLite, this always passes —
    /// proving there are no state leaks between test runs.
    /// </summary>
    [Fact]
    public async Task FreshDatabase_HasZeroOrders()
    {
        // Act
        var count = await _db.Orders.CountAsync();

        // Assert — clean slate
        Assert.Equal(0, count);
    }

    #endregion

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}

// ╔════════════════════════════════════════════════════════════════════════════╗
// ║  SQL SERVER ALTERNATIVE — How to do this with a real database server     ║
// ║                                                                         ║
// ║  In production, you use SQL Server, PostgreSQL, or MySQL — not SQLite.  ║
// ║  In-memory mode doesn't exist for these. Here are two strategies:       ║
// ╚════════════════════════════════════════════════════════════════════════════╝

#region SQL SERVER ALTERNATIVE 1: TRANSACTION ROLLBACK

// ┌─────────────────────────────────────────────────────────────────────┐
// │ Strategy: Wrap each test in a transaction, rollback at the end.    │
// │ The database never sees committed data — zero cleanup needed.      │
// │                                                                    │
// │ Pros: Fast. No Docker. Works with any DB.                         │
// │ Cons: Doesn't test COMMIT behavior (some constraint violations    │
// │       are deferred until commit time).                             │
// └─────────────────────────────────────────────────────────────────────┘
//
// public class SqlServerTransactionRollbackTests : IAsyncLifetime
// {
//     private SqlConnection _connection;
//     private DbTransaction _transaction;
//     private FoodFastDbContext _db;
//
//     public async Task InitializeAsync()
//     {
//         // Connect to a real SQL Server (local or CI)
//         _connection = new SqlConnection(
//             "Server=localhost;Database=FoodFast_Test;Trusted_Connection=true;");
//         await _connection.OpenAsync();
//
//         // Begin a transaction — nothing we do will be committed
//         _transaction = await _connection.BeginTransactionAsync();
//
//         var options = new DbContextOptionsBuilder<FoodFastDbContext>()
//             .UseSqlServer(_connection)
//             .Options;
//
//         _db = new FoodFastDbContext(options);
//         await _db.Database.UseTransactionAsync(_transaction);
//     }
//
//     [Fact]
//     public async Task CreateOrder_ThenReadBack_AllFieldsMatch()
//     {
//         var entity = new OrderEntity
//         {
//             CartSubtotal = 30.00m,
//             DistanceInKm = 6.0,
//             IsRushHour = false,
//             CreatedAt = DateTime.UtcNow
//         };
//
//         _db.Orders.Add(entity);
//         await _db.SaveChangesAsync();  // writes to DB inside the transaction
//
//         _db.ChangeTracker.Clear();
//         var loaded = await _db.Orders.FindAsync(entity.Id);
//
//         Assert.NotNull(loaded);
//         Assert.Equal(30.00m, loaded.CartSubtotal);
//         // ... same assertions as above
//     }
//
//     public async Task DisposeAsync()
//     {
//         // ROLLBACK — the DB is as if nothing happened
//         await _transaction.RollbackAsync();
//         await _connection.CloseAsync();
//         await _db.DisposeAsync();
//     }
// }

#endregion

#region SQL SERVER ALTERNATIVE 2: TESTCONTAINERS (DOCKER)

// ┌─────────────────────────────────────────────────────────────────────┐
// │ Strategy: Spin up a real SQL Server in a Docker container.         │
// │ Each test class gets its own DB — true ephemeral environment.      │
// │ This is the "Testcontainers" concept from the lecture slides.      │
// │                                                                    │
// │ Pros: Real DB engine. Tests COMMIT. Production parity.            │
// │ Cons: Requires Docker. ~5-10s startup for first test.              │
// │                                                                    │
// │ Install: dotnet add package Testcontainers.MsSql                   │
// └─────────────────────────────────────────────────────────────────────┘
//
// using Testcontainers.MsSql;
//
// public class SqlServerContainerTests : IAsyncLifetime
// {
//     private MsSqlContainer _container;
//     private FoodFastDbContext _db;
//
//     public async Task InitializeAsync()
//     {
//         // Docker spins up a real SQL Server 2022 instance
//         _container = new MsSqlBuilder()
//             .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
//             .Build();
//
//         await _container.StartAsync();
//
//         // EF Core connects to the container's random port
//         var options = new DbContextOptionsBuilder<FoodFastDbContext>()
//             .UseSqlServer(_container.GetConnectionString())
//             .Options;
//
//         _db = new FoodFastDbContext(options);
//         await _db.Database.EnsureCreatedAsync();
//     }
//
//     [Fact]
//     public async Task CreateOrder_ThenReadBack_AllFieldsMatch()
//     {
//         // Exact same test code as above — the only difference
//         // is the DbContext connects to a Docker container instead
//         // of in-memory SQLite. Same assertions, same patterns.
//
//         var entity = new OrderEntity
//         {
//             CartSubtotal = 30.00m,
//             DistanceInKm = 6.0,
//             IsRushHour = false,
//             CreatedAt = DateTime.UtcNow
//         };
//
//         _db.Orders.Add(entity);
//         await _db.SaveChangesAsync();
//
//         _db.ChangeTracker.Clear();
//         var loaded = await _db.Orders.FindAsync(entity.Id);
//
//         Assert.NotNull(loaded);
//         Assert.Equal(30.00m, loaded.CartSubtotal);
//
//         // KEY DIFFERENCE: This tests real SQL Server decimal handling,
//         // not SQLite's TEXT-based decimal. If your production DB uses
//         // DECIMAL(18,2), this test catches precision mismatches.
//     }
//
//     public async Task DisposeAsync()
//     {
//         await _db.DisposeAsync();
//         await _container.DisposeAsync();
//         // Container is destroyed — DB is gone. True ephemeral environment.
//     }
// }

#endregion
