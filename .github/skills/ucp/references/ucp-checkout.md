# UCP Checkout Capability

Capability name: `dev.ucp.shopping.checkout`  
Full spec: <https://ucp.dev/latest/specification/checkout/>

---

## Overview

Checkout sessions enable a platform to drive a buyer through the full purchase flow, with the business remaining Merchant of Record (MoR). The platform collects buyer information and hands off to the business UI only when required (`requires_escalation`).

**Business remains PCI MoR** — platforms do NOT need PCI-DSS compliance because payment credentials are handled by named payment handlers, not the platform directly.

---

## Status Lifecycle

```
incomplete  ←→  requires_escalation
    ↓
ready_for_complete
    ↓
complete_in_progress
    ↓
completed   |   canceled
```

| Status | Meaning | Platform Action |
|--------|---------|-----------------|
| `incomplete` | Missing required info or has resolvable issues | Inspect `messages`, Update Checkout |
| `requires_escalation` | Needs buyer input or buyer review | Hand off via `continue_url` |
| `ready_for_complete` | All info collected | Call Complete Checkout |
| `complete_in_progress` | Business processing | Poll Get Checkout |
| `completed` | Order placed successfully | Done (immutable) |
| `canceled` | Session invalid/expired | Start new checkout |

---

## Operations

| Operation | Purpose |
|-----------|---------|
| **Create Checkout** | Initiate session with `line_items` |
| **Get Checkout** | Poll current state |
| **Update Checkout** | Full replacement — include all fields to retain |
| **Complete Checkout** | Place the order; returns `order` in response |
| **Cancel Checkout** | Cancel any non-terminal session |

**Update Checkout is a full PUT** — if you don't include a field, it's removed. Always resend buyer, line_items, and any previously set fields.

---

## Error Handling

### Message Severity

| Severity | Meaning | Platform Action |
|----------|---------|-----------------|
| `recoverable` | Platform can fix via API | Modify inputs → Update Checkout |
| `requires_buyer_input` | Business needs info not collectable via API | Hand off via `continue_url` |
| `requires_buyer_review` | Buyer must authorize (policy/regulatory) | Hand off via `continue_url` |
| `unrecoverable` | No valid resource to act on | Retry with new resource or hand off |

Both `requires_buyer_input` and `requires_buyer_review` result in `status: requires_escalation`.

### Error Processing Algorithm

```
FILTER messages WHERE type = "error"
PARTITION into: recoverable, requires_buyer_input, requires_buyer_review, unrecoverable

IF unrecoverable → hand off via continue_url or retry with new checkout
IF recoverable   → fix each error → Update Checkout → re-evaluate
IF requires_buyer_input/review → hand off via continue_url
```

### Standard Error Codes

| Code | Meaning |
|------|---------|
| `out_of_stock` | Item unavailable |
| `item_unavailable` | Item cannot be purchased (delisted) |
| `address_undeliverable` | Cannot ship to address |
| `payment_failed` | Payment processing failed |
| `eligibility_invalid` | Eligibility claim could not be verified at completion |

---

## Warning Presentation

| Presentation | Display | Proximity to `path` | Dismissible | Escalate if cannot honor |
|-------------|---------|---------------------|-------------|--------------------------|
| `notice` (default) | MUST | MAY | MAY | — |
| `disclosure` | MUST | MUST | MUST NOT | MUST via `continue_url` |

Disclosures (allergens, regulatory notices, safety warnings) MUST be shown in proximity to the `path` field and never auto-dismissed.

---

## Continue URL

- MUST be absolute HTTPS URL
- MUST be provided when `status` = `requires_escalation`
- SHOULD be provided for all non-terminal statuses
- Platforms SHOULD prefer business-provided `continue_url` over constructing their own

---

## Totals Rendering

- Render ALL top-level `totals` entries in order — do NOT reorder or filter
- Sign: negative = subtractive (discounts), positive = additive
- Exactly one `type: "subtotal"` and one `type: "total"` required
- Verify: `sum(all except "total") == total.amount` — but render business values regardless
- Do NOT complete checkout autonomously if totals don't verify

### Well-Known Total Types

| Type | Sign | Default Label |
|------|------|---------------|
| `subtotal` | + | Subtotal |
| `discount` | − | Discount |
| `items_discount` | − | Item Discounts |
| `fulfillment` | + | Shipping |
| `tax` | + | Tax |
| `fee` | + | Fee |
| `total` | = | Total |

---

## Key Entities

### Buyer
```json
{
  "first_name": "Jane",
  "last_name": "Doe",
  "email": "jane@example.com",
  "phone_number": "+15551234567"
}
```

### Context (provisional signals — not authoritative)
```json
{
  "address_country": "US",
  "address_region": "CA",
  "postal_code": "94043",
  "language": "en",
  "currency": "USD",
  "intent": "looking for a gift under $50",
  "eligibility": ["com.example.loyalty_gold"]
}
```

### Line Item (Create)
```json
{
  "item": { "id": "sku_123" },
  "quantity": 2
}
```

### Line Item (Response)
```json
{
  "id": "li_1",
  "item": { "id": "sku_123", "title": "Widget", "price": 1999, "image_url": "..." },
  "quantity": 2,
  "totals": [{ "type": "subtotal", "amount": 3998 }]
}
```

### Postal Address
```json
{
  "street_address": "123 Main St",
  "extended_address": "Apt 4B",
  "address_locality": "Springfield",
  "address_region": "IL",
  "postal_code": "62701",
  "address_country": "US",
  "first_name": "Jane",
  "last_name": "Doe"
}
```

### Links (legal — required)
```json
[
  { "type": "privacy_policy", "url": "https://shop.example/privacy" },
  { "type": "terms_of_service", "url": "https://shop.example/terms" },
  { "type": "refund_policy", "url": "https://shop.example/refunds" }
]
```

---

## Eligibility Claims

- Buyer claims benefits via `context.eligibility` (e.g., loyalty membership)
- Business applies provisional discounts/pricing during session
- At `complete_checkout`, accepted claims MUST be verified
- Unverified claim → `type: "error"`, `code: "eligibility_invalid"`, `severity: "recoverable"`
- Platform can resolve by switching payment method or removing the claim

---

## Business Implementation Guidelines

- MUST send confirmation email after `completed`
- Logic MUST be deterministic
- MUST provide `continue_url` when `status` = `requires_escalation`
- MUST include at least one `requires_buyer_input` or `requires_buyer_review` message with `requires_escalation`
- `completed` sessions are immutable

## Platform Implementation Guidelines

- MUST use `continue_url` when `status` = `requires_escalation`
- MAY use agent to drive session; MUST hand off to trusted UI for final review/placement
- SHOULD prefer business `continue_url` over self-constructed checkout permalinks
- MAY pass `context` for localization and relevance hints
