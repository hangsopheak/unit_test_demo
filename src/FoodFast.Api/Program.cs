using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.OpenTelemetry;
using FoodFast.Api.Data;
using FoodFast.Api.Models;
using FoodFast.Core.Models;
using FoodFast.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// ── OTLP config — one endpoint for all three pillars ─────────
// Switch backend in appsettings.Development.json:
//   Aspire (local):  "Endpoint": "http://localhost:4317",  "Protocol": "grpc"
//   Grafana Cloud:   "Endpoint": "https://otlp-gateway-prod-ap-southeast-1.grafana.net/otlp",  "Protocol": "http/protobuf"
// GrafanaApiKey is stored in user-secrets, not appsettings:
//   dotnet user-secrets set "OpenTelemetry:GrafanaApiKey" "<instance-id>:<token>"
var otlpEndpoint    = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
var otlpProtocol    = builder.Configuration["OpenTelemetry:Protocol"] ?? "grpc";
var otlpApiKey      = builder.Configuration["OpenTelemetry:GrafanaApiKey"];
var useHttpProtobuf = otlpProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase);

var otlpAuthHeader = string.IsNullOrEmpty(otlpApiKey)
    ? null
    : $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(otlpApiKey))}";

// Per the SDK docs: "When using HttpProtobuf, the full URL MUST be provided, including
// the signal-specific path v1/{signal}." gRPC uses the base URL as-is.
// See: github.com/open-telemetry/opentelemetry-dotnet → src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md
void ConfigureOtlp(OtlpExporterOptions otlp, string signal)
{
    otlp.Endpoint = new Uri(useHttpProtobuf
        ? $"{otlpEndpoint.TrimEnd('/')}/v1/{signal}"
        : otlpEndpoint);
    otlp.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
    if (otlpAuthHeader is not null)
        otlp.Headers = $"Authorization={otlpAuthHeader}";
}

// ── Pillar 1: Structured Logging ─────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = useHttpProtobuf ? OtlpProtocol.HttpProtobuf : OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "FoodFast.Api"
        };
        if (otlpAuthHeader is not null)
            options.Headers = new Dictionary<string, string>
            {
                ["Authorization"] = otlpAuthHeader
            };
    }));

// ── Pillars 2 & 3: Metrics + Tracing ────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("FoodFast.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("FoodFast.Api")
        .AddOtlpExporter(o => ConfigureOtlp(o, "traces")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("FoodFast.Api")
        .AddOtlpExporter(o => ConfigureOtlp(o, "metrics")));

// ── Services ─────────────────────────────────────────────────
builder.Services.AddDbContext<FoodFastDbContext>(options =>
    options.UseSqlite("Data Source=foodfast.db"));
builder.Services.AddCors();

var app = builder.Build();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// ── Seed DB ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<FoodFastDbContext>());

// ── Telemetry primitives ─────────────────────────────────────
var pricingEngine = new DeliveryPricingEngine();
var tracer        = new ActivitySource("FoodFast.Api");
var meter         = new Meter("FoodFast.Api");

var ordersCreated   = meter.CreateCounter<long>    ("foodfast.orders.created",
                          description: "Total delivery orders successfully placed");
var paymentDuration = meter.CreateHistogram<long>  ("foodfast.payment.duration_ms",
                          unit: "ms", description: "Payment gateway response time");
var paymentFailures = meter.CreateCounter<long>    ("foodfast.payment.failures",
                          description: "Payment gateway rejections and errors");
var deliveryFeeHist = meter.CreateHistogram<double>("foodfast.delivery_fee",
                          unit: "USD", description: "Distribution of delivery fees charged");

// ── POST /api/orders ─────────────────────────────────────────
app.MapPost("/api/orders", async (CreateOrderRequest request, FoodFastDbContext db) =>
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(request.CustomerName))
        errors.Add("CustomerName is required.");
    if (request.CartSubtotal < 0)
        errors.Add("CartSubtotal must be >= 0.");
    if (request.DistanceInKm <= 0)
        errors.Add("DistanceInKm must be > 0.");
    if (request.DistanceInKm > 100)
        errors.Add("DistanceInKm must be <= 100.");

    if (errors.Count > 0)
    {
        Log.Warning("Order validation failed {@ValidationEvent}", new { errors });
        return Results.BadRequest(new { errors });
    }

    var orderCount = await db.Orders.CountAsync(
        o => o.CustomerName == request.CustomerName.Trim());

    var loyaltyTier = orderCount switch
    {
        >= 10 => "Gold",
        >= 5  => "Silver",
        _     => "Bronze"
    };

    var effectiveRushHour = request.IsRushHour && loyaltyTier != "Gold";

    // ── ProcessPayment span ──────────────────────────────────
    using var paymentSpan = tracer.StartActivity("ProcessPayment");
    paymentSpan?.SetTag("payment.customer", request.CustomerName);
    paymentSpan?.SetTag("payment.amount",   request.CartSubtotal);

    var sw = Stopwatch.StartNew();
    try
    {
        if (request.DistanceInKm > 25)
        {
            paymentSpan?.SetTag("payment.routing", "long-distance-slow");
            await Task.Delay(2000);
        }

        if (request.CustomerName.StartsWith("fail_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment gateway rejected: card declined.");

        paymentSpan?.SetTag("payment.status", "approved");
    }
    catch (Exception ex)
    {
        paymentSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
        paymentSpan?.SetTag("payment.status", "failed");
        sw.Stop();
        paymentDuration.Record(sw.ElapsedMilliseconds);
        paymentFailures.Add(1, new TagList { { "reason", "gateway_rejection" } });
        Log.Error(ex, "Payment failed {@PaymentEvent}",
            new { request.CustomerName, request.CartSubtotal });
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
    sw.Stop();
    paymentDuration.Record(sw.ElapsedMilliseconds);

    // ── CalculateFee span ────────────────────────────────────
    var deliveryOrder = new DeliveryOrder
    {
        CartSubtotal = request.CartSubtotal,
        DistanceInKm = request.DistanceInKm,
        IsRushHour   = effectiveRushHour
    };

    using var feeSpan = tracer.StartActivity("CalculateFee");
    feeSpan?.SetTag("order.distance_km",    request.DistanceInKm);
    feeSpan?.SetTag("order.is_rush_hour",   effectiveRushHour);
    feeSpan?.SetTag("customer.loyalty_tier", loyaltyTier);
    var fee = pricingEngine.CalculateFee(deliveryOrder);
    feeSpan?.SetTag("order.fee", fee);

    var entity = new OrderEntity
    {
        CustomerName = request.CustomerName.Trim(),
        CartSubtotal = request.CartSubtotal,
        DistanceInKm = request.DistanceInKm,
        IsRushHour   = effectiveRushHour,
        CreatedAt    = DateTime.UtcNow
    };

    db.Orders.Add(entity);
    await db.SaveChangesAsync();

    Log.Information("Order created {@OrderEvent}", new
    {
        entity.Id, entity.CustomerName,
        LoyaltyTier = loyaltyTier, entity.DistanceInKm,
        IsRushHour = effectiveRushHour, DeliveryFee = fee
    });

    ordersCreated.Add(1, new TagList { { "rush_hour", entity.IsRushHour.ToString() } });
    deliveryFeeHist.Record((double)fee);

    return Results.Created($"/api/orders/{entity.Id}", OrderResponse.FromEntity(entity, pricingEngine));
});

// ── GET /api/orders/{id} ─────────────────────────────────────
app.MapGet("/api/orders/{id:int}", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    return entity is null
        ? Results.NotFound(new { error = "Order not found", orderId = id })
        : Results.Ok(OrderResponse.FromEntity(entity, pricingEngine));
});

// ── GET /api/orders ──────────────────────────────────────────
app.MapGet("/api/orders", async (FoodFastDbContext db) =>
{
    var entities = await db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    return Results.Ok(entities.Select(e => OrderResponse.FromEntity(e, pricingEngine)).ToList());
});

// ── DELETE /api/orders/{id} ──────────────────────────────────
app.MapDelete("/api/orders/{id:int}", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    if (entity is null)
        return Results.NotFound(new { error = "Order not found", orderId = id });

    db.Orders.Remove(entity);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ── POST /api/orders/{id}/calculate-fee ──────────────────────
app.MapPost("/api/orders/{id:int}/calculate-fee", async (int id, FoodFastDbContext db) =>
{
    var entity = await db.Orders.FindAsync(id);
    if (entity is null)
        return Results.NotFound(new { error = "Order not found", orderId = id });

    var order = new DeliveryOrder
    {
        CartSubtotal = entity.CartSubtotal,
        DistanceInKm = entity.DistanceInKm,
        IsRushHour   = entity.IsRushHour
    };
    var fee = pricingEngine.CalculateFee(order);

    return Results.Ok(new
    {
        orderId      = entity.Id,
        customerName = entity.CustomerName,
        cartSubtotal = entity.CartSubtotal,
        distanceInKm = entity.DistanceInKm,
        isRushHour   = entity.IsRushHour,
        deliveryFee  = fee,
        total        = entity.CartSubtotal + fee
    });
});

app.Run();
