# UCP Checkout — REST Binding

Full spec: <https://ucp.dev/latest/specification/checkout-rest/>

---

## Endpoints

All relative to the base URL in the business profile (`services.dev.ucp.shopping.checkout[*].rest.endpoint`).

| Operation | Method | Path |
|-----------|--------|------|
| Create Checkout | `POST` | `/checkout-sessions` |
| Get Checkout | `GET` | `/checkout-sessions/{id}` |
| Update Checkout | `PUT` | `/checkout-sessions/{id}` |
| Complete Checkout | `POST` | `/checkout-sessions/{id}/complete` |
| Cancel Checkout | `POST` | `/checkout-sessions/{id}/cancel` |

---

## Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `UCP-Agent` | **Always** | `profile="https://platform.example/.well-known/ucp"` |
| `Content-Type` | Body requests | `application/json` |
| `Idempotency-Key` | Recommended | UUID for create/update/complete; server caches result 24h |

---

## HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 OK | Success (including business errors in `messages`) |
| 201 Created | Resource created |
| 400 Bad Request | Invalid request syntax |
| 401 Unauthorized | Auth required |
| 403 Forbidden | Authenticated but not permitted |
| 409 Conflict | Idempotency key reused with different params |
| 422 Unprocessable Entity | Profile malformed |
| 424 Failed Dependency | Profile URL valid but fetch failed |
| 429 Too Many Requests | Rate limit |
| 500/503 | Server error |

**Business errors** (e.g., out of stock) are returned as HTTP 200 with `ucp.status: "error"` or `"success"` and a `messages` array — NOT 4xx/5xx.

---

## Create Checkout

```http
POST /checkout-sessions HTTP/1.1
Host: merchant.example.com
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Content-Type: application/json
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000

{
  "line_items": [
    { "item": { "id": "sku_123" }, "quantity": 2 }
  ],
  "context": {
    "address_country": "US",
    "currency": "USD"
  }
}
```

Response:
```json
{
  "ucp": { "version": "2026-04-08", "status": "success", "payment_handlers": { "com.google.pay": [{"id": "gpay_1234"}] } },
  "id": "chk_abc123",
  "status": "incomplete",
  "currency": "USD",
  "line_items": [
    { "id": "li_1", "item": { "id": "sku_123", "title": "Widget", "price": 1999 }, "quantity": 2, "totals": [{"type": "subtotal", "amount": 3998}] }
  ],
  "totals": [
    { "type": "subtotal", "amount": 3998 },
    { "type": "total", "amount": 3998 }
  ],
  "messages": [
    { "type": "error", "code": "field_required", "path": "$.buyer.email", "content": "Email is required", "severity": "recoverable" }
  ],
  "links": [
    { "type": "privacy_policy", "url": "https://merchant.example/privacy" }
  ],
  "continue_url": "https://merchant.example/checkout/chk_abc123",
  "expires_at": "2026-04-08T12:00:00Z"
}
```

---

## Update Checkout (Buyer + Fulfillment)

**Full replacement** — include ALL fields currently set:

```http
PUT /checkout-sessions/{id} HTTP/1.1
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Content-Type: application/json

{
  "line_items": [
    { "item": { "id": "sku_123" }, "id": "li_1", "quantity": 2 }
  ],
  "buyer": {
    "email": "jane@example.com",
    "first_name": "Jane",
    "last_name": "Doe"
  },
  "fulfillment": {
    "methods": [
      {
        "type": "shipping",
        "destinations": [
          {
            "street_address": "123 Main St",
            "address_locality": "Springfield",
            "address_region": "IL",
            "postal_code": "62701",
            "address_country": "US"
          }
        ]
      }
    ]
  }
}
```

After business returns fulfillment options, select one:
```json
{
  "fulfillment": {
    "methods": [
      {
        "id": "shipping_1",
        "type": "shipping",
        "destinations": [{ "id": "dest_home", ... }],
        "selected_destination_id": "dest_home",
        "groups": [{ "id": "pkg_1", "selected_option_id": "express" }]
      }
    ]
  }
}
```

---

## Complete Checkout

```http
POST /checkout-sessions/{id}/complete HTTP/1.1
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Content-Type: application/json

{
  "payment": {
    "instruments": [
      {
        "id": "pi_gpay_5678",
        "handler_id": "gpay_1234",
        "type": "card",
        "selected": true,
        "display": {
          "brand": "mastercard",
          "last_digits": "5678",
          "description": "Google Pay •••• 5678"
        },
        "billing_address": {
          "street_address": "123 Main St",
          "address_region": "CA",
          "address_country": "US",
          "postal_code": "12345"
        },
        "credential": {
          "type": "PAYMENT_GATEWAY",
          "token": "examplePaymentMethodToken"
        }
      }
    ]
  },
  "signals": {
    "dev.ucp.buyer_ip": "203.0.113.42",
    "dev.ucp.user_agent": "Mozilla/5.0 ..."
  }
}
```

Response includes `order` field:
```json
{
  "status": "completed",
  "order": {
    "id": "ord_456",
    "label": "#1042",
    "permalink_url": "https://merchant.example/orders/ord_456"
  }
}
```

---

## Get / Cancel Checkout

```http
GET /checkout-sessions/{id} HTTP/1.1
UCP-Agent: profile="https://platform.example/.well-known/ucp"
```

```http
POST /checkout-sessions/{id}/cancel HTTP/1.1
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Content-Type: application/json

{}
```

---

## Message Signing (HTTP Message Signatures — RFC 9421)

Signed request example for Create Checkout:
```http
POST /checkout-sessions HTTP/1.1
Content-Type: application/json
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Content-Digest: sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:
Signature-Input: sig1=("@method" "@authority" "@path" "idempotency-key" "content-digest" "content-type");keyid="platform-2025"
Signature: sig1=:MEUCIQDTxNq8h7....:
```

- Request signing: include `Content-Digest` when body present; sign `@method`, `@path`, `@authority`, `content-digest`, `content-type`
- Response signing RECOMMENDED for `complete_checkout`; optional for others
- Full algorithm: <https://ucp.dev/latest/specification/signatures/>

---

## Authentication Options

| Method | Header |
|--------|--------|
| API Key | `X-API-Key: <key>` |
| OAuth 2.0 | `Authorization: Bearer <token>` |
| HTTP Message Signatures | `Signature` + `Signature-Input` + `Content-Digest` |
| Mutual TLS | TLS layer — no header |

Authentication is optional and business-determined. Some operations may be public; others require auth.
