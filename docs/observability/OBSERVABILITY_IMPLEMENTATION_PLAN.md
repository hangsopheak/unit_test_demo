# Observability Implementation Plan — FoodFast API

## Telemetry Pipeline

One request generates three types of telemetry simultaneously. All three travel to the same OTLP endpoint — swap the endpoint in config to route to any backend.

```
┌─────────────┐     HTTP POST      ┌──────────────────────────────────────────────┐
│   Postman   │ ─────────────────► │              FoodFast API                    │
└─────────────┘                    │                                              │
                                   │  Serilog ──► .WriteTo.Console()             │
                                   │          └── .WriteTo.OpenTelemetry() ──┐   │
                                   │                                          │   │
                                   │  OTel SDK ──► Metrics (Meter) ──────────┤   │
                                   │           └── Traces (ActivitySource) ───┤   │
                                   └──────────────────────────────────────────┼───┘
                                                                              │
                                                              OTLP (port 4317)│
                                                                              ▼
                                                   ┌──────────────────────────────┐
                                                   │     .NET Aspire Dashboard    │
                                                   │      localhost:18888         │
                                                   │                              │
                                                   │  ├── Structured Logs tab     │
                                                   │  ├── Metrics tab             │
                                                   │  └── Traces tab              │
                                                   └──────────────────────────────┘
                                                               │
                                                   (swap endpoint in appsettings)
                                                               │
                                                               ▼
                                                   ┌──────────────────────────────┐
                                                   │       Grafana Cloud          │
                                                   │                              │
                                                   │  ├── Loki   (logs)           │
                                                   │  ├── Tempo  (traces)         │
                                                   │  └── Mimir  (metrics)        │
                                                   └──────────────────────────────┘
```

---

## Overview

This document bridges the business specification to the actual code. It walks through instrumenting `POST /api/orders` with the full OpenTelemetry stack — structured logs, four custom metrics, and a 5-span distributed trace — and explains how to route telemetry to either the local .NET Aspire Dashboard or Grafana Cloud.

---

## Concept Mapping

| Lecture Concept | Implementation |
|---|---|
| **Pillar 1 — Logs** | `Log.Warning(...)`, `Log.Information(...)`, `Log.Error(ex, ...)` via Serilog |
| **Structured Logging** | `{@OrderEvent}` destructuring — each field independently queryable in Loki/Aspire |
| **Pillar 2 — Metrics** | 5 instruments: 1 auto (`http.server.request.duration`) + 4 custom |
| **Four Golden Signals: Traffic** | `foodfast.orders.created` counter |
| **Four Golden Signals: Latency** | `foodfast.payment.duration_ms` histogram (p50, p95, p99) |
| **Four Golden Signals: Errors** | `foodfast.payment.failures` counter |
| **Pillar 3 — Traces** | 5 spans per successful order: HTTP (auto) + 2 EF Core (auto) + 2 custom |
| **Custom Spans** | `tracer.StartActivity("ProcessPayment")` and `tracer.StartActivity("CalculateFee")` |
| **Span Tags** | `activity?.SetTag("payment.status", "approved")` — searchable in Aspire/Grafana Tempo |
| **Error Spans** | `activity?.SetStatus(ActivityStatusCode.Error, ex.Message)` — marks span red in dashboard |
| **Context Propagation** | `traceparent` response header injected by ASP.NET Core auto-instrumentation |
| **OpenTelemetry Vendor Neutrality** | Change `OpenTelemetry:Endpoint` in `appsettings.Development.json` → route to any backend |

---

## Setup

### Step 1: Add NuGet Packages

```xml
<!-- Structured logging -->
<PackageReference Include="Serilog.AspNetCore" Version="9.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />

<!-- OpenTelemetry -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.15.0-beta.1" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
```

### Step 2: Configure Serilog

```csharp
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new JsonFormatter()));
```

In `appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Step 3: Configure OpenTelemetry

```csharp
var otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
var otlpApiKey   = builder.Configuration["OpenTelemetry:GrafanaApiKey"];

void ConfigureOtlp(OtlpExporterOptions otlp)
{
    otlp.Endpoint = new Uri(otlpEndpoint);
    if (!string.IsNullOrEmpty(otlpApiKey))
        otlp.Headers = $"Authorization=Basic {Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(otlpApiKey))}";
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("FoodFast.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("FoodFast.Api")
        .AddOtlpExporter(ConfigureOtlp))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("FoodFast.Api")
        .AddOtlpExporter(ConfigureOtlp));
```

### Step 4: Declare Custom Telemetry Primitives

Declare as local variables before the endpoint definitions (captured as closures by handlers):

```csharp
var tracer = new ActivitySource("FoodFast.Api");
var meter  = new Meter("FoodFast.Api");

// Four metrics — one per Golden Signal (except Saturation)
var ordersCreated   = meter.CreateCounter<long>   ("foodfast.orders.created",
                          description: "Total delivery orders successfully placed");
var paymentDuration = meter.CreateHistogram<long> ("foodfast.payment.duration_ms",
                          unit: "ms", description: "Payment gateway response time");
var paymentFailures = meter.CreateCounter<long>   ("foodfast.payment.failures",
                          description: "Payment gateway rejections and errors");
var deliveryFeeHist = meter.CreateHistogram<double>("foodfast.delivery_fee",
                          unit: "USD", description: "Distribution of delivery fees charged");
```

---

## The Instrumented Order Flow

`POST /api/orders` runs through this sequence. Each numbered step corresponds to a span or metric event.

```
1. Input validation            → 400 + Warning log (if invalid)
2. EF Core: CountAsync         → auto span (order history lookup)
3. ProcessPayment (custom)     → custom span + paymentDuration.Record()
   └─ distanceInKm > 25        → 2s delay + payment.routing tag      [demo: slow gateway]
   └─ customerName "fail_*"    → exception + SetStatus(Error) + paymentFailures.Add() [demo: 500]
4. CalculateFee (custom)       → custom span with distance + fee tags
5. EF Core: SaveChangesAsync   → auto span (insert order)
6. Post-save                   → Information log + ordersCreated.Add() + deliveryFeeHist.Record()
```

### Key code: ProcessPayment span

```csharp
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
    paymentSpan?.SetStatus(ActivityStatusCode.Error, ex.Message); // marks span red in dashboard
    paymentSpan?.SetTag("payment.status", "failed");
    sw.Stop();
    paymentDuration.Record(sw.ElapsedMilliseconds);
    paymentFailures.Add(1, new TagList { { "reason", "gateway_rejection" } });
    Log.Error(ex, "Payment failed {@PaymentEvent}", new { request.CustomerName, request.CartSubtotal });
    return Results.Problem(detail: ex.Message, statusCode: 500);
}
sw.Stop();
paymentDuration.Record(sw.ElapsedMilliseconds); // always record — even fast failures affect p99
```

> **Why record in both catch and after?** If you only record after the try/catch, failed requests don't appear in the histogram. Your p99 looks artificially fast because you're only measuring successes.

### Key code: CalculateFee span

```csharp
using var feeSpan = tracer.StartActivity("CalculateFee");
feeSpan?.SetTag("order.distance_km",  request.DistanceInKm);
feeSpan?.SetTag("order.is_rush_hour", request.IsRushHour);
var fee = pricingEngine.CalculateFee(deliveryOrder);
feeSpan?.SetTag("order.fee", fee); // tag after calculation — records the actual result
```

> **Why `activity?.SetTag(...)`?** If no OTel listener is configured, `StartActivity` returns null. The null-conditional prevents crashes with zero overhead when OTel is disabled.

---

## Running the Demo

### Start the Local Stack

```bash
# Aspire Dashboard — UI on 18888, OTLP ingest on 4317 (forwarded to container 18889)
docker run --rm -d \
  -p 18888:18888 -p 4317:18889 \
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
  --name aspire-dashboard \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest

# API
cd src/FoodFast.Api && dotnet run
```

### The Four Scenarios

```bash
# 400 — validation failure
curl -s -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"","cartSubtotal":-1,"distanceInKm":0,"isRushHour":false}'

# 201 — happy path, 5 spans
curl -s -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Alice","cartSubtotal":45.00,"distanceInKm":8.5,"isRushHour":false}'

# 201 — slow payment (distanceInKm > 25)
curl -s -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Charlie","cartSubtotal":55.00,"distanceInKm":28,"isRushHour":false}'

# 500 — payment error (customerName starts with "fail_")
curl -s -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"fail_alice","cartSubtotal":60.00,"distanceInKm":8.5,"isRushHour":false}'
```

### Aspire Dashboard (http://localhost:18888)

| Tab | What to look for |
|---|---|
| **Structured Logs** | Filter `Level=Warning` for 400/duplicate, `Level=Error` for 500, `Level=Information` for success |
| **Metrics** | `foodfast.*` instruments + `http.server.request.duration` with all 4 status codes |
| **Traces** | Happy-path waterfall (5 spans), slow-path (ProcessPayment = 2s), error-path (red badge) |

### Switch to Grafana Cloud

Update `appsettings.Development.json`:

```json
"OpenTelemetry": {
  "Endpoint": "https://otlp-gateway-prod-xx-0.grafana.net/otlp",
  "GrafanaApiKey": "<your-instance-id>:<your-api-key>"
}
```

No code changes needed — restart the API and run the same curl commands. Explore in:
- **Loki** — `{service_name="FoodFast.Api"}` — structured logs with all fields
- **Tempo** — paste a Trace ID — same 5-span waterfall
- **Prometheus/Mimir** — `foodfast_payment_duration_ms_bucket` — histogram buckets
