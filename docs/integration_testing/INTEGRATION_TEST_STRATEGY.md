# Integration Test Strategy — FoodFast API

## Overview

This document defines the integration testing strategy for the FoodFast API. It maps each lecture concept from Session 8 (Integration Testing & API Contract) to a concrete testing approach, and details every test case in the Postman collection and WireMock test suite.

## Concept Mapping to Lecture Slides

| Lecture Concept | Testing Approach |
|---|---|
| **The "Perfect" Unit Test Fallacy** | Demonstrate: all unit tests pass, but Postman reveals a serialization bug |
| **Defining the Boundary** | Test 3 boundaries: Code↔DB (EF Core), Code↔Network (HTTP), Code↔External API (WireMock) |
| **The API Contract** | Every Postman request verifies status code + headers + body schema |
| **Dissecting the HTTP Contract** | Separate test assertions for Method, URL, Status Code, Headers, Body |
| **Postman: Collections & Environments** | Collection JSON + Environment JSON with `{{baseUrl}}` variable |
| **Postman: Test Scripts** | `pm.test()` blocks run after every response |
| **Postman: Pre-request Scripts** | `pm.environment.set("orderId", ...)` chains data between requests |
| **Postman: Collection Runner** | Run all 6 requests sequentially — full contract verification |
| **Ephemeral Environments** | Delete `foodfast.db` before each run — clean slate; in-memory SQLite per test class |
| **WireMock: Stubbing** | Pre-recorded 200 response for payment API |
| **WireMock: Verification** | Assert request body was sent correctly |
| **WireMock: Fault Injection** | Simulate 503 Service Unavailable and network timeout |
| **Strategic Data Seeding** | API-based: POST creates test data, chained to subsequent requests |
| **The Determinism Challenge** | No shared state — each collection run creates and deletes its own data |

## Part 1: Postman Collection — API Contract Tests

### Collection Architecture

The collection is organized by **endpoint**, with **Happy Path** and **Sad Path** subfolders under each. Each test case (TC-xx) is a folder that may contain multiple chained requests.

```
FoodFast API Contract Tests
│
├── POST /api/orders
│   ├── Happy Path
│   │   ├── TC-01: Standard order (6km, non-rush → $5)        [1 request]
│   │   ├── TC-02: Rush hour order (3km, rush → $3)            [1 request]
│   │   └── TC-03: Free delivery order ($60 cart → $0)         [1 request]
│   └── Sad Path
│       ├── TC-04: Negative cart subtotal → 400                [1 request]
│       ├── TC-05: Distance exceeds 100km → 400                [1 request]
│       ├── TC-06: Negative distance → 400                     [1 request]
│       ├── TC-07: Zero distance → 400                         [1 request]
│       └── TC-08: Multiple validation errors → 400            [1 request]
│
├── GET /api/orders/{id}
│   ├── Happy Path
│   │   ├── TC-09: Get standard order → 200                    [1 request]
│   │   ├── TC-10: Get rush hour order → 200                   [1 request]
│   │   └── TC-11: Get free delivery order → 200               [1 request]
│   └── Sad Path
│       └── TC-12: Non-existent order → 404                    [1 request]
│
├── GET /api/orders
│   └── Happy Path
│       └── TC-13: List all orders → 200 array, sorted         [1 request]
│
├── POST /api/orders/{id}/calculate-fee
│   ├── Happy Path
│   │   ├── TC-14: Standard fee = $5, total = $35              [1 request]
│   │   ├── TC-15: Rush fee = $3, total = $23                  [1 request]
│   │   └── TC-16: Free delivery fee = $0, total = $60         [1 request]
│   └── Sad Path
│       └── TC-17: Non-existent order → 404                    [1 request]
│
└── DELETE /api/orders/{id}
    ├── Happy Path
    │   └── TC-18: Create → Delete → Verify 404                [3 chained requests]
    └── Sad Path
        └── TC-19: Delete non-existent → 404                   [1 request]
```

### Environment Variables

| Variable | Set by | Used by |
|---|---|---|
| `{{baseUrl}}` | Environment file (pre-configured) | All requests |
| `{{standardOrderId}}` | TC-01: Create Standard Order | TC-09, TC-14 |
| `{{rushOrderId}}` | TC-02: Create Rush Hour Order | TC-10, TC-15 |
| `{{freeDeliveryOrderId}}` | TC-03: Create Free Delivery | TC-11, TC-16 |
| `{{deleteTargetId}}` | TC-18 Step 1: Create temp order | TC-18 Steps 2–3 |

---

### POST /api/orders — Happy Path

#### TC-01: Create standard order (6km, non-rush → $5 fee)

**Request:** `POST {{baseUrl}}/api/orders`
**Body:** `{ "cartSubtotal": 30.00, "distanceInKm": 6.0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 201 | HTTP contract — resource created |
| 2 | Location header matches `/api/orders/\d+` | REST convention |
| 3 | Body has `id`, `cartSubtotal`, `distanceInKm`, `isRushHour`, `deliveryFee`, `createdAt` | Schema contract |
| 4 | `deliveryFee == 5` | Medium distance tier = $5 |
| 5 | Extract `standardOrderId` | Data chaining |

#### TC-02: Create rush hour order (3km, rush → $3 fee)

**Request:** `POST {{baseUrl}}/api/orders`
**Body:** `{ "cartSubtotal": 20.00, "distanceInKm": 3.0, "isRushHour": true }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 201 | HTTP contract |
| 2 | `deliveryFee == 3` | Short tier $2 × 1.5 rush = $3 |
| 3 | Extract `rushOrderId` | Data chaining |

#### TC-03: Create free delivery order ($60 cart → $0 fee)

**Request:** `POST {{baseUrl}}/api/orders`
**Body:** `{ "cartSubtotal": 60.00, "distanceInKm": 8.0, "isRushHour": true }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 201 | HTTP contract |
| 2 | `deliveryFee == 0` | Free delivery: cart >= $50 |
| 3 | Extract `freeDeliveryOrderId` | Data chaining |

---

### POST /api/orders — Sad Path

#### TC-04: Negative cart subtotal → 400

**Body:** `{ "cartSubtotal": -10.00, "distanceInKm": 5.0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 400 | Validation rejected |
| 2 | `errors` array mentions `CartSubtotal` | Correct field identified |

#### TC-05: Distance exceeds 100km → 400

**Body:** `{ "cartSubtotal": 25.00, "distanceInKm": 150.0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 400 | Validation rejected |
| 2 | `errors` array mentions `DistanceInKm` | Correct field identified |

#### TC-06: Negative distance → 400

**Body:** `{ "cartSubtotal": 25.00, "distanceInKm": -5.0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 400 | Validation rejected |
| 2 | `errors` array mentions `DistanceInKm` | Correct field identified |

#### TC-07: Zero distance → 400

**Body:** `{ "cartSubtotal": 25.00, "distanceInKm": 0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 400 | Validation rejected |
| 2 | `errors` array mentions `DistanceInKm` | Correct field identified |

#### TC-08: Multiple validation errors → 400 with all errors

**Body:** `{ "cartSubtotal": -5.00, "distanceInKm": 200.0, "isRushHour": false }`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 400 | Validation rejected |
| 2 | `errors` array has >= 2 items | All errors returned, not just the first |
| 3 | Errors mention both `CartSubtotal` and `DistanceInKm` | Both fields identified |

---

### GET /api/orders/{id} — Happy Path

#### TC-09: Get standard order → 200

**Request:** `GET {{baseUrl}}/api/orders/{{standardOrderId}}`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Resource exists |
| 2 | `id`, `cartSubtotal`, `distanceInKm`, `isRushHour` match creation | Data persisted correctly |
| 3 | `deliveryFee == 5` | Fee correct after DB roundtrip |

#### TC-10: Get rush hour order → 200

**Request:** `GET {{baseUrl}}/api/orders/{{rushOrderId}}`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Resource exists |
| 2 | Fields match, `isRushHour == true` | Rush flag persisted |
| 3 | `deliveryFee == 3` | Surcharge correct after roundtrip |

#### TC-11: Get free delivery order → 200

**Request:** `GET {{baseUrl}}/api/orders/{{freeDeliveryOrderId}}`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Resource exists |
| 2 | `deliveryFee == 0` | Free delivery threshold held |

### GET /api/orders/{id} — Sad Path

#### TC-12: Non-existent order → 404

**Request:** `GET {{baseUrl}}/api/orders/99999`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 404 | Resource doesn't exist |
| 2 | Body has `error` and `orderId` (== 99999) | Error schema + correct ID |

---

### GET /api/orders — Happy Path

#### TC-13: List all orders → 200 array, sorted

**Request:** `GET {{baseUrl}}/api/orders`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | Response is an array | Correct shape |
| 3 | Length >= 3 | Seeded orders present |
| 4 | Sorted by `createdAt` descending | ORDER BY works |

---

### POST /api/orders/{id}/calculate-fee — Happy Path

#### TC-14: Standard fee = $5.00, total = $35

**Request:** `POST {{baseUrl}}/api/orders/{{standardOrderId}}/calculate-fee`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | Body has `orderId`, `cartSubtotal`, `distanceInKm`, `isRushHour`, `deliveryFee`, `total` | Schema contract |
| 3 | `total == cartSubtotal + deliveryFee` | Arithmetic integrity |
| 4 | `deliveryFee == 5` | Medium tier through full stack |

#### TC-15: Rush fee = $3.00, total = $23

**Request:** `POST {{baseUrl}}/api/orders/{{rushOrderId}}/calculate-fee`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | `total == cartSubtotal + deliveryFee` | Arithmetic integrity |
| 3 | `deliveryFee == 3` | Rush surcharge through full stack |

#### TC-16: Free delivery fee = $0.00, total = $60

**Request:** `POST {{baseUrl}}/api/orders/{{freeDeliveryOrderId}}/calculate-fee`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | `deliveryFee == 0` | Free delivery through full stack |
| 3 | `total == cartSubtotal` | Total = subtotal when fee is zero |

### POST /api/orders/{id}/calculate-fee — Sad Path

#### TC-17: Non-existent order → 404

**Request:** `POST {{baseUrl}}/api/orders/99999/calculate-fee`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 404 | Resource doesn't exist |
| 2 | Body has `error` and `orderId` | Error schema |

---

### DELETE /api/orders/{id} — Happy Path

#### TC-18: Create → Delete → Verify 404 (full lifecycle)

This test case chains **3 requests** to verify the complete delete lifecycle:

| Step | Request | Assertion |
|---|---|---|
| 1 | `POST /api/orders` (create temp order) | 201 Created, extract `deleteTargetId` |
| 2 | `DELETE /api/orders/{{deleteTargetId}}` | 204 No Content, empty body |
| 3 | `GET /api/orders/{{deleteTargetId}}` | 404 Not Found, error body |

**Boundary tested:** DB insert → DB delete → DB read (miss) → full lifecycle

### DELETE /api/orders/{id} — Sad Path

#### TC-19: Delete non-existent order → 404

**Request:** `DELETE {{baseUrl}}/api/orders/99999`

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 404 | Resource doesn't exist |
| 2 | Body has `error` and `orderId` | Error schema |

---

## Part 2: Database Integration Tests — Code ↔ DB Boundary

### Test Class: `DatabaseIntegrationTests`

Each test gets a **fresh in-memory SQLite database** — no shared state, no cleanup needed. This maps directly to the "Ephemeral Environments" slide.

### Ephemeral Strategy

The constructor creates a new in-memory SQLite connection and calls `EnsureCreated()`. On dispose, the connection closes and the DB vanishes. Every test starts with zero rows.

### Test Cases

| Test | What it verifies | Boundary |
|---|---|---|
| `CreateOrder_ThenReadBack_AllFieldsMatch` | All properties survive write → read roundtrip | EF Core column mapping |
| `CreateOrder_WithoutId_DatabaseGeneratesAutoIncrementId` | DB generates auto-increment ID > 0 | PRIMARY KEY AUTOINCREMENT |
| `CreateOrder_WithPreciseDecimal_PrecisionSurvivesRoundtrip` | $30.99 doesn't become $30.989999... | SQLite TEXT ↔ C# decimal |
| `CreateOrder_ThenCalculateFee_FeeMatchesExpectedValue` | Fee = $5 after data goes through DB roundtrip | Full chain: DB → domain → business logic |
| `DeleteOrder_ThenReadBack_ReturnsNull` | FindAsync returns null after delete | DB delete behavior |
| `CreateTwoOrders_GetDifferentIds` | Concurrent inserts get unique IDs | Auto-increment collision |
| `GetAllOrders_OrderedByCreatedAtDescending` | LINQ OrderBy translates to correct SQL | EF Core query translation |
| `FreshDatabase_HasZeroOrders` | New DB has zero rows — no state leaks | Ephemeral isolation |

### Key Technique: `ChangeTracker.Clear()`

After `SaveChangesAsync()`, call `_db.ChangeTracker.Clear()` before reading back. Without this, EF Core returns the cached in-memory object — you're not testing the database at all.

### SQL Server Alternatives

The test file contains two commented sections showing production DB strategies:

| Strategy | How | Speed | Tests Commit? | Needs Docker? |
|---|---|---|---|---|
| **In-memory SQLite** (active) | `Data Source=:memory:` | Fastest | N/A | No |
| **Transaction Rollback** (commented) | `BeginTransaction` → test → `Rollback` | Fast | No | No |
| **Testcontainers** (commented) | Docker spins up real SQL Server per class | Medium | Yes | Yes |

---

## Part 3: WireMock.Net — Service Virtualization Tests

### Test Class: `WireMockPaymentTests`

WireMock.Net starts a real HTTP server on a random port. Our `HttpClient` connects over TCP — the full network stack is exercised.

| Test | WireMock Feature | What it demonstrates |
|---|---|---|
| `PaymentApi_WhenChargeSucceeds_Returns200WithTransactionId` | **Stubbing** | Pre-recorded response: POST → 200 with `{ transactionId, status }` |
| `PaymentApi_WhenServiceUnavailable_Returns503` | **Fault Injection** | Simulate payment gateway outage: POST → 503 |
| `PaymentApi_WhenNetworkTimeout_ThrowsException` | **Fault Injection** | 10-second delay + 2-second client timeout → `TaskCanceledException` |
| `PaymentApi_VerifyRequestWasMadeWithCorrectBody` | **Verification** | Inspect `LogEntries` to confirm request body contained `"C001"` |

### Why WireMock over Moq?

| Aspect | Moq (Unit Test) | WireMock (Integration Test) |
|---|---|---|
| Lives where? | In-process (same memory) | Out-of-process (separate port) |
| Network stack | Bypassed entirely | Fully exercised (TCP, HTTP, DNS) |
| Serialization | Skipped | JSON serialized/deserialized |
| Timeout testing | Impossible (returns instantly) | Possible (real socket delay) |
| Fault injection | Limited (throws exceptions) | Realistic (503, 504, connection reset) |

---

## Part 4: Environment & Determinism Strategy

### Ephemeral State

- **Database:** SQLite file (`foodfast.db`) — delete before each test run
- **Postman variables:** `{{orderId}}` is set dynamically per run — no hardcoded IDs
- **WireMock:** Server starts fresh per test class — no state leaks

### Data Seeding Strategy

**API-based seeding** (safest approach from slides):
1. Request 1 (`POST /api/orders`) creates the test data
2. Test scripts extract `orderId` from the response
3. Requests 2–6 use `{{orderId}}` — no direct DB manipulation

This approach:
- Goes through the "front door" (validates the creation path too)
- Ensures data integrity (all business rules applied)
- Is portable (works against any environment, not just local)

---

## Running the Tests

### Postman (GUI)

1. Import `postman/FoodFast.postman_collection.json`
2. Import `postman/Local.postman_environment.json`
3. Select "FoodFast Local" environment
4. Open Collection Runner → Run all requests

### Postman (CLI — Newman)

```bash
newman run postman/FoodFast.postman_collection.json \
  -e postman/Local.postman_environment.json
```

### Database integration tests (xUnit)

```bash
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~DatabaseIntegration"
```

### WireMock tests (xUnit)

```bash
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~WireMock"
```

### Reset ephemeral state

```bash
rm -f src/FoodFast.Api/foodfast.db
```
