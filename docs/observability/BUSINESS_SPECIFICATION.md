# FoodFast API — Observability Specification

## Problem Statement

The FoodFast delivery API has been running in production since Session 1. It accepts orders, calculates fees, and stores data in a database — but it emits no telemetry. When something goes wrong, engineers have no way to answer basic questions:

- **Did the payment gateway slow down?** No latency data.
- **Why did an order fail?** No logs with context.
- **Which part of the request pipeline broke?** No trace to follow.

The only way to detect a problem today is when a customer complains — which means Mean Time To Detect (MTTD) is measured in complaints, not seconds.

This specification defines the observability layer added to `POST /api/orders`: what to log, what to measure, and what to trace — so that any failure in the order pipeline can be detected and diagnosed without waiting for user feedback.

## Overview

Instruments the `POST /api/orders` endpoint with OpenTelemetry to provide full production observability: structured logs (the "What"), metrics (the "How Much"), and distributed traces (the "Where"). The instrumentation uses the vendor-neutral OpenTelemetry standard, routeable to any compatible backend — the .NET Aspire Dashboard locally or Grafana Cloud in production — with a single config change.

---

## Telemetry Model

### Logs

Structured JSON events emitted via Serilog. Each event is machine-parsable and independently filterable by field.

| Property | Type | Description |
|---|---|---|
| Timestamp | string (ISO 8601) | UTC time the event occurred |
| Level | string | `Information`, `Warning`, or `Error` |
| MessageTemplate | string | Serilog message template with named properties |
| Properties | object | Destructured domain fields specific to the event |

### Metrics

| Metric Name | Instrument | Unit | Description |
|---|---|---|---|
| `http.server.request.duration` | Histogram | s | Auto — request latency by route + status code |
| `foodfast.orders.created` | Counter | — | Successful orders placed (tagged by `rush_hour`) |
| `foodfast.payment.duration_ms` | Histogram | ms | Payment gateway response time (happy + error paths) |
| `foodfast.payment.failures` | Counter | — | Payment gateway rejections (tagged by `reason`) |
| `foodfast.delivery_fee` | Histogram | USD | Distribution of delivery fees on successful orders |

### Traces

All spans belong to a single trace rooted at the incoming HTTP request.

| Span | Source | Key Tags |
|---|---|---|
| `POST /api/orders` | ASP.NET Core auto | `http.method`, `http.route`, `http.response.status_code` |
| `EF: Orders.CountAsync` | EF Core auto | SQL query text, db name |
| `ProcessPayment` | Custom (`FoodFast.Api`) | `payment.customer`, `payment.amount`, `payment.status`, `payment.routing` (slow path) |
| `CalculateFee` | Custom (`FoodFast.Api`) | `order.distance_km`, `order.is_rush_hour`, `order.fee`, `customer.loyalty_tier` |
| `EF: SaveChangesAsync` | EF Core auto | SQL query text, db name |

---

## Business Rules

### Loyalty Tier

The customer's total order history determines their tier, which affects delivery fee calculation.

| Tier | Condition | Benefit |
|---|---|---|
| Bronze | Fewer than 5 past orders | Standard pricing |
| Silver | 5–9 past orders | Standard pricing |
| Gold | 10 or more past orders | Rush-hour surcharge waived |

The tier is resolved by querying the customer's order count at the time of placement. This drives the `CalculateFee` span — the `customer.loyalty_tier` tag shows which tier was applied and whether `is_rush_hour` was overridden.

---

## Instrumentation Rules

### Log Events

| Trigger | Level | Key Fields |
|---|---|---|
| Validation fails (400) | `Warning` | `errors` (list) |
| Payment exception (500) | `Error` | `CustomerName`, `CartSubtotal`, full exception |
| Order created successfully | `Information` | `Id`, `CustomerName`, `LoyaltyTier`, `DistanceInKm`, `IsRushHour`, `DeliveryFee` |

### Metric Increment Rules

| Condition | Metric | Change |
|---|---|---|
| Order created (201) | `foodfast.orders.created` | +1, tagged `rush_hour` |
| Payment completes (success or fail) | `foodfast.payment.duration_ms` | Record elapsed ms |
| Payment exception | `foodfast.payment.failures` | +1, tagged `reason=gateway_rejection` |
| Order created (201) | `foodfast.delivery_fee` | Record fee value in USD |

### Span Lifecycle Rules

| Condition | Span | Behaviour |
|---|---|---|
| Exception in payment | Parent HTTP span | Inherits error status from child span |
| Validation or duplicate fail | No DB or custom spans | Trace stops at HTTP span (no unnecessary work performed) |

---

## Demo Simulation Scenarios

> **Note for students:** The following behaviours are **intentionally simulated** for teaching purposes. They do not represent real FoodFast business logic — they are controlled triggers that let us observe specific telemetry patterns in a classroom setting without needing a real payment gateway or production traffic.

| Trigger | What it simulates | Why it's useful to observe |
|---|---|---|
| `distanceInKm > 25` | A slow third-party payment gateway (2s response time) | Shows how a trace waterfall immediately reveals which span is the bottleneck — without guessing |
| `customerName` starts with `"fail_"` | A payment gateway rejection (card declined) | Shows how an error propagates through the span hierarchy and how `Log.Error` captures the full exception context |

---

## Example Scenarios

### Scenario 1 — 400 Bad Request

| Input | `customerName: ""`, `cartSubtotal: -1`, `distanceInKm: 0` |
|---|---|
| HTTP status | 400 |
| Log | `Warning`: validation failed, errors list |
| Metrics | No change |
| Trace spans | HTTP span only (status 400), no DB or payment spans |

### Scenario 2 — Happy Path

| Input | `customerName: "Alice"`, `cartSubtotal: 45.00`, `distanceInKm: 8.5` |
|---|---|
| HTTP status | 201 Created |
| Log | `Information`: order created, `LoyaltyTier=Bronze`, `DeliveryFee=5.00` |
| Metrics | `orders.created` +1, `payment.duration_ms` ~40ms, `delivery_fee` $5.00 |
| Trace spans | HTTP → CountAsync (loyalty lookup) → ProcessPayment → CalculateFee → SaveChangesAsync |
| Note | `CalculateFee` span shows `customer.loyalty_tier=Bronze`, `order.is_rush_hour=false` |

### Scenario 3 — Slow Payment Gateway

| Input | `customerName: "Charlie"`, `distanceInKm: 28` |
|---|---|
| HTTP status | 201 Created |
| Log | `Information`: order created |
| Metrics | `payment.duration_ms` ~2040ms (visible spike in histogram) |
| Trace spans | `ProcessPayment` span = 2040ms, `payment.routing=long-distance-slow` tag |

### Scenario 4 — 500 Payment Error

| Input | `customerName: "fail_alice"`, `cartSubtotal: 60.00` |
|---|---|
| HTTP status | 500 Internal Server Error |
| Log | `Error`: payment failed with full exception |
| Metrics | `payment.failures` +1, `payment.duration_ms` recorded |
| Trace spans | HTTP (error) → AnyAsync → ProcessPayment (error, no CalculateFee or SaveChanges) |
