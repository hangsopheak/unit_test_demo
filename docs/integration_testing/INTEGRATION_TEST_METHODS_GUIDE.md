# Integration Test Methods — How They Fit Together

## The Three Methods at a Glance

| Method | What It Tests | Boundary Crossed | Speed | Fidelity |
|--------|--------------|-------------------|-------|----------|
| **Postman (API contract tests)** | HTTP contract: status codes, headers, JSON shape, business logic through the full stack | Network + Serialization + DB | Slow (real HTTP) | Highest — tests what the client actually sees |
| **Database integration tests** | Persistence logic: EF Core mappings, queries, ordering, schema correctness | Code → Database | Medium (in-memory SQLite) | High for data layer, but skips HTTP |
| **WireMock (service virtualization)** | Behavior when external dependencies fail, slow down, or return unexpected data | Code → External Service | Fast (stubbed HTTP) | High for resilience, but the "external service" is fake |

---

## How They Complement Each Other

```
                    Client
                      |
                      v
              +---------------+
              |   HTTP Layer   | <-- Postman tests (contract, status codes, headers)
              +-------+-------+
                      |
              +-------v-------+
              | Business Logic | <-- Unit tests (sessions 1-7, already covered)
              +-------+-------+
                      |
           +----------+----------+
           v                     v
   +---------------+    +---------------+
   |   Database     |    | External APIs  |
   |   (SQLite)     |    | (Payment, etc) |
   +---------------+    +---------------+
         ^                       ^
   DB integration          WireMock tests
      tests              (service virtualization)
```

Each method owns a **different boundary**. Together they cover all the integration seams without redundant overlap.

---

## The "Not Either/Or" Principle

These methods are not alternatives — they are layers.

| Scenario | Postman | DB Test | WireMock |
|----------|:-------:|:-------:|:--------:|
| POST returns 201 with correct JSON shape | Yes | — | — |
| Order persists with correct fields | — | Yes | — |
| Fee calculation survives HTTP round-trip | Yes | — | — |
| Orders return sorted by CreatedAt DESC | Maybe | Yes (focused) | — |
| Payment API timeout → graceful fallback | — | — | Yes |
| Payment API 500 → order still created | — | — | Yes |
| Delete removes row from DB | — | Yes | — |
| DELETE returns 204 then GET returns 404 | Yes (TC-18) | — | — |

Notice: some rows have only one checkmark. That is the point — each method has a **unique coverage zone** that the others cannot easily reach.

---

## What Breaks If You Skip One?

Remove one method and see what slips through undetected:

| If You Skip... | What Slips Through Undetected | Real-World Consequence |
|---|---|---|
| **Postman** | JSON field renamed `deliveryFee` → `delivery_fee`, 201 returned instead of 204, `Location` header missing | Mobile app crashes in production — API contract broke silently |
| **Database tests** | EF Core maps `decimal` to `float`, `ORDER BY` missing, soft-delete doesn't filter | Data corruption, wrong totals on invoices, "deleted" orders still appear |
| **WireMock** | Payment API timeout → unhandled exception, 500 from provider → app crashes | 3 AM outage because a third-party API went down and your app had no fallback |

---

## Cost-Confidence Tradeoff

Not all tests are equal in effort vs. payoff:

```
                         High Confidence
                              ^
                              |
                   +----------+----------+
                   |       Postman        |
                   |  (full stack, slow,  |
                   | highest confidence)  |
                   +----------+----------+
                              |
                   +----------+----------+
                   |   DB Integration     |
                   | (real queries, medium)|
                   +----------+----------+
                              |
                   +----------+----------+
                   |      WireMock        |
                   | (controlled failure, |
                   |    fast, focused)    |
                   +----------+----------+
                              |
                   +----------+----------+
                   |     Unit Tests       |
                   | (fastest, narrowest) |
                   +----------+----------+
                              |
                              v
                         Low Confidence

        Low Setup Cost <--------------> High Setup Cost
```

- **Unit tests**: Cheapest to write, fastest to run, but blind to integration boundaries
- **WireMock**: Low setup (stub a few endpoints), fast, but only tests your reaction to their failure
- **DB integration**: Moderate setup (schema + seed data), catches persistence bugs that unit tests mock away
- **Postman**: Highest setup (running server, environment variables, chaining), but tests exactly what the client experiences

---

## The "Trust Boundary" Mental Model

Every integration test method exists because of a **trust boundary** — a point where you stop trusting your own code and start trusting someone else's:

```
+----------------------------------------------+
|              Your Code                        |
|  +----------------------------------------+  |
|  |  Business Logic (unit tested)          |  |
|  +------------------+---------------------+  |
|                     |                        |
|          +----------+----------+             |
|          |                     |             |
|          v                     v             |
|  +---------------+    +---------------+      |
|  |  ASP.NET Core |    |  EF Core +    |      |
|  |  (serializer, |    |  SQLite       |      |
|  |   routing)    |    |  (ORM, SQL)   |      |
|  +-------+-------+    +-------+-------+      |
|          |                     |             |
+----------------------------------------------+
           |                     |
   - - - - | - - - - - - - - - - | - - - - - -   <-- System boundaries
           |                     |
   +-------v-------+    +-------v-------+
   |  HTTP Client   |    | External API   |
   |  (Postman)     |    | (WireMock)     |
   +---------------+    +---------------+
```

The rule: **test at every boundary where data changes format or ownership**.

- JSON to/from C# object → Postman catches serialization mismatches
- C# object to/from SQL row → DB tests catch ORM mapping bugs
- Your API to/from Their API → WireMock catches unhandled failure modes

