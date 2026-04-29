# FoodFast API — Performance Testing with K6

## Step-by-Step Setup Guide

### Step 1 — Install K6

**macOS:**
```bash
brew install k6
```

**Windows:**
```bash
choco install k6
```

**Linux (Debian/Ubuntu):**
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
  --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491466396D8
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

**Verify installation:**
```bash
k6 version
```

### Step 2 — Start the API

K6 sends real HTTP requests to a running server. Start the FoodFast API first:

```bash
cd src/FoodFast.Api
dotnet run
```

The API runs on `http://localhost:5000` and seeds 100 orders on startup.

**Verify it's running** — open a new terminal and test:
```bash
curl http://localhost:5000/api/orders
```

You should see a JSON array of orders.

### Step 3 — Create the Project Structure

From the **repository root**, create the K6 folder structure:

```bash
mkdir -p perf/k6/helpers
```

This gives you:
```
perf/
  k6/
    helpers/      ← shared config goes here
                  ← test scripts go here
```

### Step 4 — Create the Helper Module

Create `perf/k6/helpers/config.js` — this is shared by all test scripts:

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

**What this does:**
- `BASE_URL` — single place to change if the API port changes
- `HEADERS` — JSON content type for POST requests
- `randomOrder()` — generates a different order payload each call (varied names, prices, distances, rush hour)

### Step 5 — Create Your First Script (Smoke Test)

Create `perf/k6/smoke-test.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, HEADERS, randomOrder } from './helpers/config.js';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const res = http.post(`${BASE_URL}/api/orders`, randomOrder(), { headers: HEADERS });

  check(res, {
    'status is 201': (r) => r.status === 201,
    'has id': (r) => r.json('id') !== undefined,
  });

  sleep(1);
}
```

**Key concepts in this script:**

| Concept | Code | Purpose |
|---|---|---|
| `options` | `vus: 1, duration: '30s'` | 1 virtual user for 30 seconds |
| `thresholds` | `p(95)<500` | Fail the test if 95th percentile latency exceeds 500ms |
| `check()` | `'status is 201': ...` | Assert each response (logged, doesn't stop test) |
| `sleep(1)` | Think time | Simulates a real user pausing between actions |

### Step 6 — Run Your First Test

From the **repository root**:

```bash
k6 run perf/k6/smoke-test.js
```

**Expected output:**
```
     checks.........................: 100.00% ✓ 30   ✗ 0
     http_req_duration..............: avg=8ms   p(95)=15ms
   ✓ { p(95)<500 }
     http_req_failed................: 0.00%    ✓ 0    ✗ 30
   ✓ { rate<0.01 }
     http_reqs......................: 30      1.0/s
     iterations.....................: 30      1.0/s
```

If you see `✓` next to thresholds — the test passed. Your setup is correct.

### Step 7 — Add More Test Scripts

Follow the same pattern to create the remaining scripts (see Test Scripts section below for configurations):

| Script | Create this file | Copy config from |
|---|---|---|
| Load | `perf/k6/load-test.js` | Load Test section |
| Stress | `perf/k6/stress-test.js` | Stress Test section |
| Spike | `perf/k6/spike-test.js` | Spike Test section |
| Soak | `perf/k6/soak-test.js` | Soak Test section |
| Full Workflow | `perf/k6/full-workflow.js` | Full Workflow section |

### Step 8 — Run with Real-Time Dashboard (Optional)

K6 has a built-in web dashboard for live charts during the test:

```bash
K6_WEB_DASHBOARD=true k6 run perf/k6/stress-test.js
# Open http://localhost:5665 in your browser
```

### Resetting Between Tests

Each test creates orders in the database. To start fresh:

```bash
rm src/FoodFast.Api/foodfast.db
cd src/FoodFast.Api && dotnet run
```

---

## System Under Test

**5 endpoints** on a SQLite-backed API (port 5000, 100 seeded orders on startup). SQLite uses file-level locking — only one writer at a time — making performance zones visible at modest load.

| Method | Endpoint | Write/Read | Concern |
|---|---|---|---|
| POST | `/api/orders` | Write | Lock contention under concurrent writes |
| GET | `/api/orders` | Read | Response grows as orders accumulate (no pagination) |
| GET | `/api/orders/{id}` | Read | Fast, but affected by DB lock waits |
| DELETE | `/api/orders/{id}` | Write | Same lock contention as POST |
| POST | `/api/orders/{id}/calculate-fee` | Read | CPU-bound: runs `DeliveryPricingEngine` |

## Project Structure

```
perf/k6/
  helpers/config.js       — shared BASE_URL, HEADERS, randomOrder()
  smoke-test.js           — 1 VU, 30s — sanity check
  load-test.js            — ramp to 100 VUs — expected peak traffic
  stress-test.js          — ramp to 2000 VUs — find breaking point
  spike-test.js           — 10 → 300 VUs in 10s — sudden burst
  soak-test.js            — 30 VUs, 10 min — endurance / degradation
  full-workflow.js        — Create → Get → Calculate Fee → Delete lifecycle
```

## Helper Module (`helpers/config.js`)

- `BASE_URL`: `http://localhost:5000`
- `HEADERS`: `Content-Type: application/json`
- `randomOrder()`: Generates varied payloads (8 names, $5–$85 cart, 1–31 km, ~33% rush hour)

## Test Scripts — Quick Reference

### Smoke Test — "Does it even work?"

| Setting | Value |
|---|---|
| VUs | 1 |
| Duration | 30s |
| Thresholds | p95 < 500ms, error rate < 1% |
| Endpoints | POST `/api/orders` |
| Think time | 1s |

Baseline sanity check. All metrics should be excellent.

### Load Test — "Will it handle expected peak?"

| Stage | Duration | Target VUs |
|---|---|---|
| Ramp up | 30s | 20 |
| Ramp to peak | 1m | 100 |
| Hold at peak | 30s | 100 |
| Ramp down | 30s | 0 |

- **Thresholds:** p95 < 500ms, error rate < 5%
- **Endpoints:** POST + GET `/api/orders`
- **Think time:** 1s
- **What to observe:** Latency climbs with VUs; throughput may plateau at capacity.

### Stress Test — "When will it break?"

| Stage | Duration | Target VUs |
|---|---|---|
| Zone 1 healthy | 30s | 200 |
| Slow ramp to collapse | 60s | 2000 |
| Recovery | 30s | 0 |

- **Thresholds:** p95 < 500ms, error rate < 5%
- **Endpoints:** POST `/api/orders`
- **Think time:** none (no sleep)
- **What to observe:** SQLite lock contention causes errors to spike at high VU counts. The slow ramp makes the exact breaking point visible.

### Spike Test — "Flash sale at 12:00 PM"

| Stage | Duration | Target VUs |
|---|---|---|
| Normal traffic | 10s | 10 |
| SPIKE | 10s | 300 |
| Sustained spike | 30s | 300 |
| Spike ends | 10s | 10 |
| Recovery | 30s | 10 |

- **Thresholds:** p95 < 3000ms (relaxed)
- **Endpoints:** POST `/api/orders`
- **Think time:** 0.3s (minimal — everyone clicking at once)
- **Key observation:** How quickly does the system **recover** after the spike ends?

### Soak Test — "Does it degrade over time?"

| Stage | Duration | Target VUs |
|---|---|---|
| Ramp up | 1m | 30 |
| Sustained | 8m | 30 |
| Ramp down | 1m | 0 |

- **Thresholds:** p95 < 500ms, error rate < 5%
- **Endpoints:** POST + GET `/api/orders`
- **Think time:** 1s
- **Degradation trigger:** GET `/api/orders` returns ALL orders — response grows as test creates more. This surfaces slow degradation (memory leaks, growing payloads).

> Production soak tests run 1–4 hours; 10 minutes is shortened for demo.

### Full Workflow — "Which step is the bottleneck?"

| Stage | Duration | Target VUs |
|---|---|---|
| Ramp up | 30s | 20 |
| Hold | 1m | 20 |
| Ramp down | 30s | 0 |

- **Thresholds:** p95 < 800ms, error rate < 5%
- **Workflow per iteration:** Create → Get → Calculate Fee → Delete (uses `group()` for per-step metrics)
- **Self-cleaning:** Deletes each order at end — no data accumulation

Expected relative speed: GET (fastest) > Calculate Fee (CPU) > POST/DELETE (write lock contention)

## Performance Curve — Expected Zones

| Zone | VUs | Latency | Errors | State |
|---|---|---|---|---|
| 1: Healthy | 1–50 | ~5–50ms | 0% | Spare capacity |
| 2: Saturation | 50–200 | 50–500ms | 0–5% | Latency climbing, throughput plateaus |
| 3: Collapse | 200+ | 500ms–2s+ | 20–50%+ | Lock contention dominates |

## Checks vs Thresholds

| | Checks | Thresholds |
|---|---|---|
| Scope | Per-request | Whole test |
| On failure | Logged, test continues | Test exits with code 1 |
| Analogy | `Assert.Equal()` | CI pass/fail gate |

## Key K6 Metrics

| Metric | K6 Name | Measures |
|---|---|---|
| Latency | `http_req_duration` (avg, p95) | Response time |
| Throughput | `http_reqs` rate | Requests per second |
| Error rate | `http_req_failed` rate | Failed request percentage |
| Check rate | `checks` rate | Assertion pass percentage |

## Execution

```bash
# Prerequisites
brew install k6                          # macOS
cd src/FoodFast.Api && dotnet run        # Start API on :5000

# Run tests (in recommended order)
k6 run perf/k6/smoke-test.js             # 30s
k6 run perf/k6/load-test.js              # ~2.5 min
k6 run perf/k6/stress-test.js            # ~2 min
k6 run perf/k6/spike-test.js             # ~1.5 min
k6 run perf/k6/full-workflow.js          # ~2 min
k6 run perf/k6/soak-test.js              # ~10 min

# With real-time dashboard
K6_WEB_DASHBOARD=true k6 run perf/k6/stress-test.js
# Open http://localhost:5665

# Reset database between tests
rm src/FoodFast.Api/foodfast.db
```
