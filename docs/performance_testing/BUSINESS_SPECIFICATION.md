# FoodFast API — Performance Testing Specification

## Overview

The **FoodFast API** is tested for performance using K6, a JavaScript-based load testing tool. The API remains unchanged from sessions 8–9 — the same 5 endpoints, the same SQLite database, the same `DeliveryPricingEngine`. The difference is the question being asked: previous sessions asked "does it work correctly?" — this session asks "does it work correctly **under pressure?**"

## System Under Test

### API Endpoints

| Method | Endpoint | Write/Read | Performance Concern |
|---|---|---|---|
| POST | `/api/orders` | Write | SQLite file lock contention under concurrent writes |
| GET | `/api/orders` | Read | Response payload grows as orders accumulate (no pagination) |
| GET | `/api/orders/{id}` | Read | Single-row lookup — fast, but affected by DB lock waits |
| DELETE | `/api/orders/{id}` | Write | Same lock contention as POST |
| POST | `/api/orders/{id}/calculate-fee` | Read | CPU-bound: runs `DeliveryPricingEngine.CalculateFee()` per request |

### Database: SQLite

SQLite uses **file-level locking** — only one writer at a time. This is the key performance bottleneck:

| Concurrency | SQLite Behavior |
|---|---|
| 1 writer | No contention — fast |
| 10 writers | Writers queue behind the lock — latency increases |
| 50+ writers | Lock contention dominates — errors and timeouts appear |
| 200+ writers | Effective throughput drops — system is "up" but unusable |

This limitation is **intentional for this demo** — it makes the Performance Curve (Zones 1–3) visible at modest load levels that K6 can generate on a single laptop.

### Seed Data

The API seeds **100 orders** on startup (fixed random seed for reproducibility). This means:
- `GET /api/orders` returns 100 items initially
- As K6 creates more orders, this list grows and the response gets larger
- This simulates real-world data growth during a soak test

## Performance Requirements (Thresholds)

### Smoke Test (Baseline)

| Metric | Threshold | Rationale |
|---|---|---|
| p95 latency | < 500ms | Single user should get fast responses |
| Error rate | < 1% | No errors expected at zero contention |

### Load Test (Expected Peak)

| Metric | Threshold | Rationale |
|---|---|---|
| p95 latency | < 500ms | SLO: 95% of users get sub-500ms response at peak |
| Error rate | < 5% | Small error budget for occasional lock timeouts |

### Stress Test (Breaking Point)

| Metric | Threshold | Rationale |
|---|---|---|
| p95 latency | < 2000ms | Relaxed — we expect degradation |
| Error rate | < 50% | Relaxed — goal is to find limits, not pass |

### Spike Test (Sudden Burst)

| Metric | Threshold | Rationale |
|---|---|---|
| p95 latency | < 3000ms | Very relaxed — spike conditions |

### Soak Test (Endurance)

| Metric | Threshold | Rationale |
|---|---|---|
| p95 latency | < 500ms | Same as load test — sustained performance |
| Error rate | < 5% | No degradation expected at moderate sustained load |

## K6 Test Types

### 1. Smoke Test — "Does it even work?"

- **Virtual Users (VUs):** 1
- **Duration:** 30 seconds
- **Purpose:** Baseline sanity check before running heavier tests
- **Expected result:** All checks pass, latency under 50ms, 0% errors

### 2. Load Test — "Will it handle expected traffic?"

- **VUs:** Ramp to 50 over stages
- **Duration:** ~2.5 minutes
- **Stage shape:** Ramp up → Peak → Hold → Ramp down
- **Purpose:** Verify the API meets its SLO (p95 < 500ms) under expected peak traffic
- **Expected result:** Latency increases slightly, most checks pass

### 3. Stress Test — "When will it break?"

- **VUs:** Ramp to 200 over stages
- **Duration:** ~2.5 minutes
- **Stage shape:** Normal → Beyond normal → Pushing limits → Breaking point → Recovery
- **Purpose:** Find the exact load level where the system transitions from Zone 1 (Healthy) to Zone 2 (Saturation) to Zone 3 (Collapse)
- **Expected result:** Errors increase sharply at high VU counts, latency explodes, SQLite lock contention visible

### 4. Spike Test — "Flash sale at 12:00 PM"

- **VUs:** 10 → 300 in 10 seconds
- **Duration:** ~1.5 minutes
- **Stage shape:** Normal → Instant spike → Sustained spike → Recovery
- **Purpose:** Simulate a sudden traffic burst (viral post, ticket sale, flash promotion)
- **Expected result:** System overwhelmed during spike, key observation is whether it **recovers** after spike ends

### 5. Soak Test — "The marathon"

- **VUs:** 30 sustained
- **Duration:** 10 minutes (shortened for demo; production soak = 1–4 hours)
- **Purpose:** Find slow degradation: memory leaks, connection pool exhaustion, growing response sizes
- **Expected result:** `GET /api/orders` gets progressively slower as orders accumulate (no pagination)

### 6. Full Workflow — "The user journey under load"

- **VUs:** Ramp to 20
- **Duration:** ~2 minutes
- **Workflow:** Create → Get → Calculate Fee → Delete (complete lifecycle per iteration)
- **Purpose:** Test the full user journey under concurrent load, see which step is the bottleneck
- **Expected result:** POST and DELETE (writes) are slower than GET (read) due to SQLite locking

## Key Metrics

| Metric | K6 Name | What It Measures | Slides Anchor |
|---|---|---|---|
| Latency (average) | `http_req_duration` avg | Mean response time | "The Time Factor" |
| Latency (p95) | `http_req_duration` p(95) | 95th percentile — worst 5% experience | Performance Curve |
| Throughput | `http_reqs` rate | Requests per second | "The Volume Factor" |
| Error rate | `http_req_failed` rate | Percentage of failed requests | USE Method: Errors |
| Check pass rate | `checks` rate | Percentage of passed assertions | Functional correctness under load |
| Iterations | `iterations` rate | Complete test cycles per second | Full workflow throughput |

## The Performance Curve — Expected Observations

### Zone 1: Healthy (Smoke Test, 1 VU)

- Latency: ~5–20ms average
- Throughput: ~30–50 req/s
- Errors: 0%
- System state: "Flow" — resources have spare capacity

### Zone 2: Saturation (Load Test, 50 VUs)

- Latency: ~50–200ms average, p95 climbing
- Throughput: plateaus — more VUs don't increase req/s
- Errors: 0–5% (occasional SQLite lock timeouts)
- System state: "Stress" — one spike away from collapse

### Zone 3: Collapse (Stress Test, 200 VUs)

- Latency: 500ms–2000ms+ average, p95 may exceed thresholds
- Throughput: drops — system spends time managing failures
- Errors: 20–50%+ (SQLite lock contention, timeouts)
- System state: "Unresponsive" — effectively dead to users

> **Note:** Exact numbers vary based on machine hardware, background processes, and OS scheduler. The pattern (stable → climbing → explosion) is consistent.

## Example Scenarios

| Test Type | VUs | Expected p95 Latency | Expected Error Rate | Key Observation |
|---|---|---|---|---|
| Smoke | 1 | < 30ms | 0% | Baseline — everything works |
| Load (ramp) | 20 → 50 | 50–200ms | < 5% | Latency climbs with VU count |
| Stress (peak) | 200 | 500ms–2s+ | 20–50% | SQLite lock contention — Zone 3 |
| Spike (burst) | 10 → 300 | 1s–3s+ | 30–60% | System overwhelmed, recovery time observable |
| Soak (sustained) | 30 | Starts ~100ms, climbs over time | < 5% | `GET /api/orders` degrades as data grows |
| Full workflow | 20 | POST ~100ms, GET ~20ms | < 5% | Writes slower than reads (DB locking) |
