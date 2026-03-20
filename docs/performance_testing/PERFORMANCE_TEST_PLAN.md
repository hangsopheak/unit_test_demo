# Performance Testing Plan — FoodFast API with K6

## Overview

This document defines the performance testing strategy for the FoodFast API using K6. It maps each lecture concept to a concrete K6 script, details the script architecture, and specifies every test type with its configuration, purpose, and expected behavior.

## How to Use This Document

1. **Read the concept mapping** to understand which slide topic each K6 script demonstrates
2. **Study the helper module** to understand shared configuration and data generation
3. **Review each test type** — they are ordered from lightest (smoke) to heaviest (soak)
4. **Cross-reference with the Business Specification** for expected metrics and thresholds

---

## Part 1: Concept Mapping to Lecture Slides

### The Physics of Traffic

| Concept | K6 Script | How It Demonstrates |
|---|---|---|
| Zone 1: Healthy | `smoke-test.js` | 1 VU — stable latency, 0% errors, predictable throughput |
| Zone 2: Saturation | `load-test.js` | 50 VUs — latency climbing, throughput plateaus, queuing begins |
| Zone 3: Collapse | `stress-test.js` | 200 VUs — errors spike, throughput drops, system unresponsive |
| The "Knee" of the curve | `stress-test.js` at ~100 VUs | Transition point visible in web dashboard as latency curve bends upward |

### Testing Methodologies

| Concept | K6 Script | Configuration |
|---|---|---|
| Load Testing ("Will it work?") | `load-test.js` | `stages` ramp to 50 VUs — expected peak traffic |
| Stress Testing ("When will it break?") | `stress-test.js` | `stages` ramp to 200 VUs — beyond expected limits |
| Spike Testing (Flash Sale) | `spike-test.js` | 10 → 300 VUs in 10 seconds — sudden burst |
| Soak Testing (Endurance) | `soak-test.js` | 30 VUs for 10 minutes — find slow degradation |

### The USE Method

| USE Dimension | K6 Metric | What to Watch |
|---|---|---|
| Utilization | System CPU during test | CPU spikes during stress/spike tests |
| Saturation | `http_req_waiting` (time spent queued) | Grows as SQLite lock contention increases |
| Errors | `http_req_failed` rate | Jumps from 0% → 20%+ at saturation point |

### K6 Core Concepts

| Concept | Where It Appears | Purpose |
|---|---|---|
| **Checks** | Every script — `check(res, {...})` | Per-request assertions (like `Assert.Equal` in xUnit) |
| **Thresholds** | Every script — `options.thresholds` | Whole-test pass/fail criteria (SLO enforcement) |
| **Stages** | Load, stress, spike, soak scripts | Define the VU ramp-up/ramp-down traffic shape |
| **Groups** | `full-workflow.js` — `group('name', fn)` | Organize metrics by workflow step |
| **Think time** | Every script — `sleep(n)` | Simulate realistic user pauses between actions |

---

## Part 2: Project Structure

```
perf/
  k6/
    helpers/
      config.js           — shared base URL, headers, random order generator
    smoke-test.js         — 1 VU, 30s — sanity check
    load-test.js          — ramp to 50 VUs — expected peak traffic
    stress-test.js        — ramp to 200 VUs — find breaking point
    spike-test.js         — 0 → 300 VUs — sudden traffic burst
    soak-test.js          — 30 VUs, 10 min — endurance / degradation
    full-workflow.js      — Create → Get → Calculate → Delete lifecycle
```

### Why this structure?

- **One file per test type** — each script answers a different performance question
- **Shared helper** — `config.js` centralizes the base URL, headers, and data generator so scripts stay DRY
- **Separate from `tests/`** — performance tests are not unit/integration tests; they live in their own `perf/` folder

---

## Part 3: Helper Module

**File:** `perf/k6/helpers/config.js`

```javascript
export const BASE_URL = 'http://localhost:5000';

export const HEADERS = {
  'Content-Type': 'application/json',
};

const NAMES = ['Alice', 'Bob', 'Charlie', 'Dave', 'Eve', 'Frank', 'Grace', 'Hank'];

export function randomOrder() {
  return JSON.stringify({
    customerName: NAMES[Math.floor(Math.random() * NAMES.length)],
    cartSubtotal: Math.round((Math.random() * 80 + 5) * 100) / 100,
    distanceInKm: Math.round((Math.random() * 30 + 1) * 10) / 10,
    isRushHour: Math.random() < 0.33,
  });
}
```

**Key design decisions:**

- `randomOrder()` generates a different payload each call — avoids cache effects and exercises the full input space
- `NAMES` array with 8 entries provides realistic variety without being excessive
- Cart subtotal range ($5–$85) spans below and above the $50 free delivery threshold
- Distance range (1–31 km) spans all three pricing tiers (< 5, 5–10, >= 10 km)
- ~33% rush hour probability exercises both pricing paths

---

## Part 4: Test Scripts — Detailed

### Script 1: Smoke Test

**File:** `perf/k6/smoke-test.js`

**Question answered:** "Does the API even work?"

| Setting | Value | Rationale |
|---|---|---|
| VUs | 1 | Single user — no contention |
| Duration | 30s | Enough iterations for a meaningful sample |
| p95 threshold | < 500ms | Generous — should be well under this |
| Error threshold | < 1% | Zero errors expected |

**K6 building blocks introduced:**

| Concept | Code | Purpose |
|---|---|---|
| `options` | `{ vus: 1, duration: '30s', thresholds: {...} }` | Define load shape and pass/fail criteria |
| `check()` | `check(res, { 'status is 201': ... })` | Assert each response (logged, doesn't fail test) |
| `thresholds` | `{ http_req_duration: ['p(95)<500'] }` | Fail the test if latency exceeds SLO |
| `sleep()` | `sleep(1)` | Simulate user think time between actions |

**What to observe:** All metrics should be excellent. This establishes the baseline for comparison with heavier tests.

---

### Script 2: Load Test

**File:** `perf/k6/load-test.js`

**Question answered:** "Will it handle expected peak traffic?"

| Stage | Duration | Target VUs | Simulates |
|---|---|---|---|
| Ramp up | 30s | 20 | Morning traffic building |
| Ramp to peak | 1m | 50 | Lunch hour peak |
| Hold at peak | 30s | 50 | Sustained peak load |
| Ramp down | 30s | 0 | Evening wind-down |

**Endpoints exercised:** POST `/api/orders` (create) + GET `/api/orders` (list)

**What to observe:**
- Latency increases as VUs ramp up — this is the approach to Zone 2
- Throughput (req/s) may plateau even as VUs increase — system is at capacity
- The web dashboard shows the "knee" of the Performance Curve
- The list endpoint (`GET /api/orders`) gets slightly slower as more orders accumulate

---

### Script 3: Stress Test

**File:** `perf/k6/stress-test.js`

**Question answered:** "When will it break?"

| Stage | Duration | Target VUs | Zone |
|---|---|---|---|
| Normal load | 30s | 50 | Zone 1: Healthy |
| Beyond normal | 30s | 100 | Zone 1 → Zone 2 transition |
| Pushing limits | 30s | 150 | Zone 2: Saturation |
| Breaking point | 30s | 200 | Zone 3: Collapse |
| Recovery | 30s | 0 | Does it come back? |

**Thresholds are intentionally relaxed** — the goal is to observe failure, not prevent it.

**What to observe:**
- SQLite lock contention becomes visible at ~100 VUs (error rate starts climbing)
- At 200 VUs, the `http_req_failed` rate may exceed 30%
- The recovery stage shows whether the system returns to normal after load drops
- p95 latency may exceed 1–2 seconds at peak stress

**SQLite-specific failure mode:** SQLite throws "database is locked" errors when too many concurrent writers compete for the file lock. This is not a bug — it's a design limitation that makes the performance curve visible at modest load levels.

---

### Script 4: Spike Test

**File:** `perf/k6/spike-test.js`

**Question answered:** "Can it handle a sudden burst?"

| Stage | Duration | Target VUs | What Happens |
|---|---|---|---|
| Normal traffic | 10s | 10 | Baseline |
| SPIKE | 10s | 300 | Instant burst — the "flash sale" |
| Sustained spike | 30s | 300 | Maintained pressure |
| Spike ends | 10s | 10 | Traffic drops |
| Recovery | 30s | 10 | Does the system recover? |

**What to observe:**
- The transition from 10 → 300 VUs is nearly instant — the system has no time to adapt
- Error rate and latency spike simultaneously
- The **recovery period** is the most important observation: how long until metrics return to pre-spike levels?
- If recovery is slow, it indicates queued requests are still being processed (the "Thundering Herd" aftermath)

---

### Script 5: Soak Test

**File:** `perf/k6/soak-test.js`

**Question answered:** "Does it degrade over time?"

| Stage | Duration | Target VUs | Purpose |
|---|---|---|---|
| Ramp up | 1m | 30 | Gentle start |
| Sustained | 8m | 30 | Look for degradation |
| Ramp down | 1m | 0 | Clean shutdown |

**Why this test is different:** The load is *moderate and constant*. The stress test pushes hard for 2 minutes; the soak test pushes gently for 10 minutes. Different failures surface:

| Failure Type | Stress Test Finds It? | Soak Test Finds It? |
|---|---|---|
| Lock contention | Yes | Sometimes |
| Memory leaks | No | Yes |
| Connection pool exhaustion | No | Yes |
| Growing response payloads | No | Yes |
| Disk space exhaustion | No | Yes |

**Built-in degradation trigger:** The script hits both `POST /api/orders` (creates data) and `GET /api/orders` (returns ALL data). As the test runs, the GET response grows — no pagination means every order ever created is returned. This simulates real-world data growth.

> **Note:** 10 minutes is a shortened duration for classroom demo. Production soak tests run 1–4 hours to surface slow leaks.

---

### Script 6: Full Workflow

**File:** `perf/k6/full-workflow.js`

**Question answered:** "Which step in the user journey is the bottleneck?"

| Step | Endpoint | Method | Expected Relative Speed |
|---|---|---|---|
| Create Order | `/api/orders` | POST | Slowest (write lock) |
| Get Order | `/api/orders/{id}` | GET | Fastest (single read) |
| Calculate Fee | `/api/orders/{id}/calculate-fee` | POST | Medium (read + CPU) |
| Delete Order | `/api/orders/{id}` | DELETE | Slow (write lock) |

**K6 `group()` feature:** Each step is wrapped in a `group()` call, which separates metrics by step in the K6 output. This lets you see exactly which endpoint is the bottleneck.

**Self-cleaning:** The workflow deletes each order at the end, preventing data accumulation. This is the opposite of the soak test, where accumulation is intentional.

---

## Part 5: Reading K6 Output

### Terminal Summary

Every K6 run prints a summary table. Here's how to read the key lines:

```
     checks.........................: 97.50%  ✓ 1950     ✗ 50
     http_req_duration..............: avg=45ms   min=3ms   med=28ms   max=1.2s   p(90)=95ms   p(95)=180ms
   ✓ { p(95)<500 }
     http_req_failed................: 2.50%   ✓ 50       ✗ 1950
   ✓ { rate<0.05 }
     http_reqs......................: 2000    66.7/s
     iterations.....................: 1000    33.3/s
```

| Line | Read as |
|---|---|
| `checks: 97.50%` | 97.5% of individual response assertions passed |
| `http_req_duration avg=45ms` | Average response time was 45ms |
| `p(90)=95ms p(95)=180ms` | 90% of requests under 95ms, 95% under 180ms |
| `✓ { p(95)<500 }` | Threshold PASSED — p95 is under 500ms |
| `http_req_failed: 2.50%` | 2.5% of requests returned errors |
| `✓ { rate<0.05 }` | Threshold PASSED — error rate under 5% |
| `http_reqs: 2000 66.7/s` | 2000 total requests at 66.7 per second throughput |
| `iterations: 1000 33.3/s` | 1000 complete test cycles (each cycle may make multiple requests) |

### Percentiles Explained

| Percentile | Meaning | Who experiences it |
|---|---|---|
| p(50) / median | Half of requests are faster than this | The "typical" user |
| p(90) | 90% of requests are faster than this | Most users |
| p(95) | 95% are faster — this is the standard SLO metric | The industry standard |
| p(99) | Only 1% are slower — the "tail latency" | Your angriest user |

> **Rule of thumb:** Average hides outliers. p95 is the standard for SLOs because it captures the experience of the vast majority while allowing for natural variation.

### K6 Web Dashboard

Run any script with `K6_WEB_DASHBOARD=true` to get real-time charts:

```bash
K6_WEB_DASHBOARD=true k6 run perf/k6/stress-test.js
# Open http://localhost:5665 in browser
```

The dashboard shows:
- **VUs over time** — matches the `stages` configuration
- **Request rate** — throughput in real-time
- **Response time** — latency percentiles as a time series (watch the curve form!)
- **Errors** — error rate over time
- **Checks** — pass/fail rate live

---

## Part 6: Checks vs Thresholds

These two K6 features serve different purposes. Understanding the distinction is essential.

### Checks — Per-Request Assertions

```javascript
check(res, {
  'status is 201': (r) => r.status === 201,
  'has id': (r) => r.json('id') !== undefined,
});
```

- Evaluated on **every response**
- Failures are **logged** but do not fail the test
- Think of them as `Assert.Equal()` — they verify correctness
- Useful for: "Did this specific request return the right status code?"

### Thresholds — Whole-Test Pass/Fail

```javascript
export const options = {
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.05'],
  },
};
```

- Evaluated **after the test completes** against aggregated metrics
- Failures cause K6 to exit with code 1 (test **fails**)
- Think of them as a CI gate — they determine if the build is green or red
- Useful for: "Did the overall performance meet our SLO?"

### Summary

| | Checks | Thresholds |
|---|---|---|
| Scope | Single request | Entire test run |
| On failure | Logged, test continues | Test exits with code 1 |
| Analogy | `Assert.Equal()` | CI pass/fail gate |
| Question | "Was this response correct?" | "Did the system meet its SLO?" |

---

## Part 7: Test Execution

### Prerequisites

```bash
# Install K6
brew install k6          # macOS
choco install k6         # Windows
# See full installation guide in the demo plan

# Start the API
cd src/FoodFast.Api
dotnet run
# API runs on http://localhost:5000 with 100 seeded orders
```

### Run individual scripts

```bash
k6 run perf/k6/smoke-test.js        # 30 seconds
k6 run perf/k6/load-test.js         # ~2.5 minutes
k6 run perf/k6/stress-test.js       # ~2.5 minutes
k6 run perf/k6/spike-test.js        # ~1.5 minutes
k6 run perf/k6/full-workflow.js      # ~2 minutes
k6 run perf/k6/soak-test.js         # ~10 minutes
```

### Run with Web Dashboard

```bash
K6_WEB_DASHBOARD=true k6 run perf/k6/stress-test.js
# Open http://localhost:5665 for real-time charts
```

### Reset database between tests

```bash
rm src/FoodFast.Api/foodfast.db
cd src/FoodFast.Api && dotnet run
# Fresh database with 100 seeded orders
```

### Recommended test order

1. **Smoke** — verify the API works before applying load
2. **Load** — check performance under expected peak
3. **Stress** — find the breaking point
4. **Spike** — test sudden burst resilience
5. **Full Workflow** — identify per-endpoint bottlenecks
6. **Soak** — check for degradation over time (run last, takes longest)
