# E2E Test Plan — FoodFast Order Management UI

## Overview

This document defines the End-to-End testing strategy for the FoodFast frontend using Playwright. It maps each lecture concept to a concrete test implementation, details the Page Object Model architecture, and specifies every test case with its purpose and expected behavior.

## How to Use This Document

1. **Read the concept mapping** to understand which slide topic each test demonstrates
2. **Study the Page Object Model** to understand how selectors are abstracted
3. **Review each test case** — they are grouped by the user journey they verify
4. **Cross-reference with the Business Specification** for expected values and rules

---

## Part 1: Concept Mapping to Lecture Slides

### Section 1: The Economics of Regression

| Concept | How E2E Tests Address It |
|---|---|
| Quality Wall | Automated E2E tests prevent the "80% re-testing" trap — run 6 test files in seconds |
| Regression Overhead | Each test verifies a complete user journey — new features can't silently break existing flows |
| Shift-Right Strategy | E2E tests act as the final gatekeeper before deployment |

### Section 2: The E2E Paradigm

| Concept | Test Implementation |
|---|---|
| Full-stack verification (no mocks) | `create-order.spec.ts` — real browser, real React, real API, real SQLite |
| Critical Path Coverage | Order creation (revenue-critical), deletion (destructive action), fee calculation (business logic) |
| Revenue-critical paths | Create order happy path — if this breaks, no orders can be placed |
| Identity-critical paths | Not applicable (no auth in FoodFast) |
| High-frequency paths | Order table rendering — every user sees this on every page load |

### Section 3: Combating Test Fragility

| Concept | Test Implementation |
|---|---|
| Brittle selectors (CSS classes) | React components use `data-testid`, never CSS classes for test targeting |
| Robust selectors (data-testid) | Every interactive element has a `data-testid` — see Business Specification |
| ARIA roles | `getByRole('button')`, `getByRole('checkbox')` used where appropriate |
| Page Object Model (POM) | `OrderPage.ts` encapsulates all selectors and actions |
| DRY testing | All 6 test files reuse `OrderPage` methods — no duplicated selectors |

### Section 4: The Flakiness Problem

| Concept | Test Implementation |
|---|---|
| Asynchronous behavior | Playwright auto-wait handles all timing — no `waitForTimeout()` anywhere |
| State isolation | API Backdoor (`request.post()`) seeds data without UI interaction |
| Data collisions | Each test that needs data seeds its own via the API, independent of other tests |

### Section 6: E2E Tooling — Playwright

| Feature | Where It's Demonstrated |
|---|---|
| Auto-waiting | Every `expect()` and `click()` — Playwright waits for visibility/stability automatically |
| Browser contexts | Each test runs in an isolated browser context (Playwright default) |
| Trace Viewer | `playwright.config.ts` enables trace capture on failure |
| Network interception | `network-interception.spec.ts` — `page.route()` stubs API responses |
| Multi-browser | Config defines Chromium, Firefox, and WebKit projects |
| HTML report | `npx playwright show-report` after test run |

---

## Part 2: Page Object Model Architecture

### Why POM?

Without POM, every test file contains raw selectors:

```typescript
// WITHOUT POM — fragile, duplicated
await page.getByTestId('input-subtotal').fill('30');
await page.getByTestId('input-distance').fill('6');
await page.getByTestId('submit-order').click();
```

With POM, tests read like user stories:

```typescript
// WITH POM — readable, maintainable
await orderPage.createOrder(30, 6, false);
```

### OrderPage Class Design

**File:** `e2e/pages/OrderPage.ts`

| Property / Method | Type | Purpose |
|---|---|---|
| `customerNameInput` | Locator | `data-testid="input-customer-name"` |
| `subtotalInput` | Locator | `data-testid="input-subtotal"` |
| `distanceInput` | Locator | `data-testid="input-distance"` |
| `rushHourCheckbox` | Locator | `data-testid="input-rush-hour"` |
| `submitButton` | Locator | `data-testid="submit-order"` |
| `ordersTable` | Locator | `data-testid="orders-table"` |
| `emptyState` | Locator | `data-testid="empty-state"` |
| `errorMessage` | Locator | `data-testid="error-message"` |
| `goto()` | Method | Navigates to the app root (`/`) |
| `createOrder(name, subtotal, distance, rushHour)` | Method | Fills form and clicks submit |
| `deleteOrder(orderId)` | Method | Clicks delete button for a specific order |
| `calculateFee(orderId)` | Method | Clicks calculate fee button for a specific order |
| `getOrderRow(orderId)` | Method | Returns locator for a specific order row |

### Selector Strategy

All selectors follow this priority (from Playwright best practices):

1. **`getByTestId()`** — primary strategy, uses `data-testid` attributes
2. **`getByRole()`** — for standard HTML elements (buttons, checkboxes)
3. **`getByText()`** — for verifying visible text content in assertions
4. **Never:** CSS classes, XPath, or DOM structure

---

## Part 3: Test Cases

### Test File 1: `create-order.spec.ts`

**User Journey:** Creating delivery orders through the form.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **Happy path** — standard order | E2E Paradigm: Full-Stack | Fill: "Alice", $30, 6km, non-rush → Submit | Order in table: Alice, $30.00, 6 km, $5.00 fee |
| **Validation error** — negative subtotal | Critical Path: Error handling | Fill: "Alice", -$10, 6km → Submit | Error message contains "CartSubtotal" |

**What it proves:** The full stack works end-to-end. Browser → React form → fetch POST → .NET API → SQLite → GET response → React table render.

---

### Test File 2: `delete-order.spec.ts`

**User Journey:** Removing an order from the system.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **Delete order** — disappears from table | API Backdoor + E2E | Seed via API → Navigate → Click delete | Order row no longer visible |

**What it proves:** Destructive actions work through the UI and the DOM updates immediately.

**Technique highlight:** Uses `request.post()` (API Backdoor) to seed test data, avoiding form interaction in the Arrange step.

---

### Test File 3: `calculate-fee.spec.ts`

**User Journey:** Viewing the delivery fee breakdown for an order.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **Rush hour fee** — breakdown shows correct values | E2E + Business Logic | Seed rush order via API → Navigate → Click calculate | Fee: $3.00, Total: $23.00 |

**What it proves:** Business logic (DeliveryPricingEngine) results are correctly displayed in the UI. The same $3.00 fee verified by unit tests and Postman is now verified through the user's eyes.

---

### Test File 4: `empty-state.spec.ts`

**User Journey:** Loading the page with no data, and verifying persistence.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **Empty state** — no orders message | E2E: Edge case | Clean DB via API → Navigate | "No orders yet" visible, table not visible |
| **Refresh persistence** — data survives reload | E2E: Full-Stack | Create order via form → Reload page | Order still visible after refresh |

**What it proves:** The UI handles empty data gracefully, and data persists in SQLite (not just React state).

---

### Test File 5: `api-backdoor.spec.ts`

**User Journey:** Demonstrating the API Backdoor pattern for state isolation.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **Seed 3 orders via API** — verify all appear in UI | State Isolation | POST 3 orders (with names) via `request` → Navigate | All 3 orders visible in table with customer names |
| **Isolated cleanup** — delete seeded data | State Isolation | POST order → Test → DELETE order | No leftover data from this test |

**What it proves:** Test data can be seeded instantly via the API instead of clicking through the UI. This is faster (milliseconds vs seconds) and more reliable (fewer failure points).

**The pattern:**
```
1. Seed data via request.post()    ← API Backdoor (fast, reliable)
2. Navigate and test via page      ← UI verification (what the user sees)
3. Cleanup via request.delete()    ← Isolation (no shared state)
```

---

### Test File 6: `network-interception.spec.ts`

**User Journey:** Testing frontend error handling when the API fails.

| Test Case | Slide Concept | Steps | Expected Result |
|---|---|---|---|
| **API 500 error** — frontend shows error gracefully | Network Interception | Stub POST /api/orders → 500 → Submit form | Error message visible, no crash |
| **Slow network** — delayed response handling | Network Interception | Stub GET with 3s delay → Navigate | Page loads eventually, no crash |

**What it proves:** The frontend handles API failures gracefully. `page.route()` intercepts requests at the browser level — the React app thinks the API returned a 500, but the real API is untouched.

**Connection to Week 8:** `page.route()` is the browser-side equivalent of WireMock. WireMock intercepts at the server level; `page.route()` intercepts at the browser level. Same concept (service virtualization), different layer.

---

## Part 4: Playwright Configuration

### Browser Matrix

| Browser | Engine | Why Include |
|---|---|---|
| Chromium | Blink | Most popular browser engine (~65% market share) |
| Firefox | Gecko | Second engine — catches Chromium-specific bugs |
| WebKit | WebKit | Safari's engine — catches iOS/macOS-specific bugs |

### Key Configuration Decisions

| Setting | Value | Rationale |
|---|---|---|
| `timeout` | 30000ms | Generous timeout for CI environments |
| `retries` | 0 | Flaky tests should fail loudly — fix the root cause, don't retry |
| `trace` | `on-first-retry` | Capture trace only when debugging failures |
| `screenshot` | `only-on-failure` | Visual evidence of failures for debugging |
| `baseURL` | `http://localhost:5173` | Vite dev server default port |
| `webServer.command` | `npm run dev` | Auto-start Vite when running tests |

### Trace Viewer

When a test fails with trace enabled, Playwright captures:
- Every user action (click, fill, navigate)
- Every network request and response
- DOM snapshots at each step
- Console logs and errors

Open with: `npx playwright show-trace test-results/<test>/trace.zip`

---

## Part 5: Test Execution

### Run all tests

```bash
cd e2e_test_demo
npx playwright test
```

### Run by test file

```bash
npx playwright test create-order
npx playwright test delete-order
npx playwright test calculate-fee
npx playwright test empty-state
npx playwright test api-backdoor
npx playwright test network-interception
```

### Run specific browser

```bash
npx playwright test --project=chromium
npx playwright test --project=firefox
npx playwright test --project=webkit
```

### Interactive UI mode

```bash
npx playwright test --ui
```

### View HTML report

```bash
npx playwright show-report
```

---

## Test Coverage Matrix

This matrix shows which business rules (from the Business Specification) are covered by which test file:

| Business Rule | create-order | delete-order | calculate-fee | empty-state | api-backdoor | network-interception |
|---|---|---|---|---|---|---|
| Create order (happy path) | x | | | | | |
| Create order (validation) | x | | | | | |
| List orders | | | | | x | |
| Delete order | | x | | | | |
| Calculate fee | | | x | | | |
| Empty state | | | | x | | |
| Page refresh persistence | | | | x | | |
| API error handling | | | | | | x |
| Slow network handling | | | | | | x |
