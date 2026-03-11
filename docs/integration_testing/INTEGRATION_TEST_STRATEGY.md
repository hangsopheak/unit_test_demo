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

### Test Architecture

```
Collection Run (sequential)
│
├── Request 1: POST /api/orders         → Seeds data, extracts {{orderId}}
├── Request 2: GET /api/orders/{{orderId}} → Verifies persistence
├── Request 3: POST /api/orders/{{orderId}}/calculate-fee → Verifies business logic
├── Request 4: GET /api/orders           → Verifies list endpoint
├── Request 5: DELETE /api/orders/{{orderId}} → Verifies deletion
└── Request 6: GET /api/orders/{{orderId}} → Verifies 404 after delete
```

### Request 1: Create Order

**Endpoint:** `POST {{baseUrl}}/api/orders`

**Body:**
```json
{
  "cartSubtotal": 30.00,
  "distanceInKm": 6.0,
  "isRushHour": false
}
```

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 201 | HTTP contract — resource was created |
| 2 | Location header matches `/api/orders/\d+` | REST convention — client can follow the link |
| 3 | Body has `id` (number), `cartSubtotal` (number), `distanceInKm` (number), `isRushHour` (boolean), `deliveryFee` (number), `createdAt` (string) | JSON schema contract |
| 4 | `deliveryFee >= 0` | Business rule: fee never negative |
| 5 | Extract `orderId` to environment | Data chaining for subsequent requests |

**Boundary tested:** JSON deserialization → DB insert → DB read → JSON serialization → HTTP 201 + Location header

---

### Request 2: Get Order by ID

**Endpoint:** `GET {{baseUrl}}/api/orders/{{orderId}}`

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Resource exists |
| 2 | `id` matches `{{orderId}}` | Correct record returned |
| 3 | `cartSubtotal` is 30.00, `distanceInKm` is 6.0, `isRushHour` is false | Data persisted correctly |
| 4 | `deliveryFee > 0` | Fee was calculated (not zero for this order) |

**Boundary tested:** DB read → domain mapping → fee calculation → JSON serialization

---

### Request 3: Calculate Fee

**Endpoint:** `POST {{baseUrl}}/api/orders/{{orderId}}/calculate-fee`

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | Body has `orderId`, `deliveryFee`, `total` | Schema contract |
| 3 | `total == cartSubtotal + deliveryFee` | Business rule: total math is correct |
| 4 | `deliveryFee == 5.00` | Specific business rule: 6km, non-rush = $5 (medium distance) |

**Boundary tested:** DB read → domain model conversion → DeliveryPricingEngine → JSON serialization

---

### Request 4: List All Orders

**Endpoint:** `GET {{baseUrl}}/api/orders`

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 200 | Endpoint works |
| 2 | Response is an array | Correct data shape |
| 3 | Array length >= 1 | At least the order we created exists |

**Boundary tested:** DB query (SELECT all) → collection serialization

---

### Request 5: Delete Order

**Endpoint:** `DELETE {{baseUrl}}/api/orders/{{orderId}}`

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 204 | Resource deleted, no body |
| 2 | Response body is empty | 204 contract — no content |

**Boundary tested:** DB delete → HTTP 204 (no body convention)

---

### Request 6: Get Deleted Order (404)

**Endpoint:** `GET {{baseUrl}}/api/orders/{{orderId}}`

**Test Assertions:**

| # | Assertion | What it verifies |
|---|---|---|
| 1 | Status code is 404 | Resource no longer exists |
| 2 | Body has `error` and `orderId` properties | Error response schema |

**Boundary tested:** DB read (miss) → 404 error contract

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
