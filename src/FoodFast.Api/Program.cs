using Microsoft.EntityFrameworkCore;
using FoodFast.Api.Data;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// SQLite — a single file on disk. Students can see it appear.
builder.Services.AddDbContext<FoodFastDbContext>(options =>
    options.UseSqlite("Data Source=foodfast.db"));

var app = builder.Build();

// Ensure DB exists on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FoodFastDbContext>();
    db.Database.EnsureCreated();
}

var pricingEngine = new DeliveryPricingEngine();

// ── POST /api/orders ──────────────────────────────────────────
// Contract: JSON body → 201 Created + Location header + body
//           OR 400 Bad Request if validation fails
app.MapPost("/api/orders", async (CreateOrderRequest request, FoodFastDbContext db) =>
{
    // Input validation — matches DeliveryPricingEngine constraints
    var errors = new List<string>();
    if (request.CartSubtotal < 0)
        errors.Add("CartSubtotal must be >= 0.");
    if (request.DistanceInKm <= 0)
        errors.Add("DistanceInKm must be > 0.");
    if (request.DistanceInKm > 100)
        errors.Add("DistanceInKm must be <= 100.");

    if (errors.Count > 0)
        return Results.BadRequest(new { errors });

    var entity = new OrderEntity
    {
        CartSubtotal = request.CartSubtotal,
        DistanceInKm = request.DistanceInKm,
        IsRushHour = request.IsRushHour,
        CreatedAt = DateTime.UtcNow
    };

    db.Orders.Add(entity);
    await db.SaveChangesAsync();

    var response = MapToResponse(entity, pricingEngine);
    return Results.Created($"/api/orders/{entity.Id}", response);
});

// ── GET /api/orders/{id} ──────────────────────────────────────
// Contract: 200 OK + body OR 404 Not Found
app.MapGet("/api/orders/{id:int}", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    if (entity is null)
        return Results.NotFound(new { error = "Order not found", orderId = id });

    return Results.Ok(MapToResponse(entity, pricingEngine));
});

// ── GET /api/orders ───────────────────────────────────────────
// Contract: 200 OK + array (possibly empty)
app.MapGet("/api/orders", async (FoodFastDbContext db) =>
{
    var entities = await db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    var responses = entities.Select(e => MapToResponse(e, pricingEngine)).ToList();
    return Results.Ok(responses);
});

// ── DELETE /api/orders/{id} ───────────────────────────────────
// Contract: 204 No Content OR 404 Not Found
app.MapDelete("/api/orders/{id:int}", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    if (entity is null)
        return Results.NotFound(new { error = "Order not found", orderId = id });

    db.Orders.Remove(entity);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ── POST /api/orders/{id}/calculate-fee ───────────────────────
// Contract: 200 OK + fee breakdown OR 404 Not Found
app.MapPost("/api/orders/{id:int}/calculate-fee", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    if (entity is null)
        return Results.NotFound(new { error = "Order not found", orderId = id });

    var order = new DeliveryOrder
    {
        CartSubtotal = entity.CartSubtotal,
        DistanceInKm = entity.DistanceInKm,
        IsRushHour = entity.IsRushHour
    };

    var fee = pricingEngine.CalculateFee(order);

    return Results.Ok(new
    {
        orderId = entity.Id,
        cartSubtotal = entity.CartSubtotal,
        distanceInKm = entity.DistanceInKm,
        isRushHour = entity.IsRushHour,
        deliveryFee = fee,
        total = entity.CartSubtotal + fee
    });
});

app.Run();

// ── Helper ────────────────────────────────────────────────────
static OrderResponse MapToResponse(OrderEntity entity, DeliveryPricingEngine engine)
{
    var order = new DeliveryOrder
    {
        CartSubtotal = entity.CartSubtotal,
        DistanceInKm = entity.DistanceInKm,
        IsRushHour = entity.IsRushHour
    };

    return new OrderResponse
    {
        Id = entity.Id,
        CartSubtotal = entity.CartSubtotal,
        DistanceInKm = entity.DistanceInKm,
        IsRushHour = entity.IsRushHour,
        DeliveryFee = engine.CalculateFee(order),
        CreatedAt = entity.CreatedAt
    };
}

// ── DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Request body for creating a delivery order.
/// </summary>
public record CreateOrderRequest(decimal CartSubtotal, double DistanceInKm, bool IsRushHour);

/// <summary>
/// Response body returned for delivery orders.
/// </summary>
public class OrderResponse
{
    public int Id { get; set; }
    public decimal CartSubtotal { get; set; }
    public double DistanceInKm { get; set; }
    public bool IsRushHour { get; set; }
    public decimal DeliveryFee { get; set; }
    public DateTime CreatedAt { get; set; }
}
