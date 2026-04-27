# UCP Overview — Profiles, Discovery, Negotiation, Versioning

Full spec: <https://ucp.dev/latest/specification/overview/>

---

## Profiles

### Business Profile — `/.well-known/ucp`

Served as JSON at a well-known path. Describes capabilities, services, and payment handlers the business supports.

```json
{
  "ucp": { "version": "2026-04-08" },
  "capabilities": {
    "dev.ucp.shopping.checkout": [
      {
        "version": "2026-04-08",
        "extends": []
      },
      {
        "version": "2026-04-08",
        "extends": [{ "name": "dev.ucp.shopping.fulfillment" }]
      }
    ]
  },
  "services": {
    "dev.ucp.shopping.checkout": [
      {
        "id": "checkout_rest",
        "transport": "rest",
        "rest": { "endpoint": "https://business.example/api/ucp" }
      }
    ]
  },
  "payment_handlers": {
    "com.google.pay": [
      {
        "id": "gpay_1234",
        "version": "2026-04-08",
        "config": { "environment": "TEST", "merchant_id": "1234" }
      }
    ]
  }
}
```

### Platform Profile — `/.well-known/ucp` (Platform)

Platforms SHOULD also host a UCP profile (though simpler — primarily capabilities and identity).

Every request from a platform MUST include:
```
UCP-Agent: profile="https://platform.example/.well-known/ucp"
```

---

## Discovery Flow

1. Platform fetches `https://business.example.com/.well-known/ucp`
2. Business returns profile listing capabilities + services + payment handlers
3. Platform runs negotiation to find mutually supported capabilities/versions
4. Platform selects matching service transport (REST/MCP/A2A/Embedded)
5. Platform sends requests to the service endpoint with `UCP-Agent` header

---

## Negotiation — Intersection Algorithm

Negotiation finds the **highest mutually supported version** for each capability:

```
for each capability in platform.capabilities ∩ business.capabilities:
    versions_intersection = platform.versions ∩ business.versions
    if versions_intersection is empty → capability not supported
    else → use max(versions_intersection)
```

- Both sides must support the same named capability AND at least one shared version
- Extensions are separate capabilities — negotiated independently
- Unrecognized capabilities/versions are ignored (not an error)

---

## Versioning

- Versions are date strings: `YYYY-MM-DD`
- Each capability versions independently
- Current latest: `2026-04-08`
- Backwards compatible within a capability's major lifecycle
- Platforms and businesses list ALL versions they support; negotiation picks the best match

---

## Namespace Governance

All capability names use **reverse-domain format**:

| Prefix | Owner |
|--------|-------|
| `dev.ucp.*` | UCP Working Group (standard capabilities) |
| `com.example.*` | Business-specific extensions |
| `org.school.*` | Organization-specific |

- Eligibility claims (`context.eligibility`) MUST use reverse-domain format, e.g. `com.example.loyalty_gold`
- Signal keys (in `signals` object) MUST use reverse-domain format to prevent collisions

Standard capability names:
- `dev.ucp.shopping.checkout`
- `dev.ucp.shopping.cart`
- `dev.ucp.shopping.catalog`
- `dev.ucp.shopping.order`
- `dev.ucp.identity.linking`

Extension names:
- `dev.ucp.shopping.fulfillment` (extends checkout)
- `dev.ucp.shopping.discount`   (extends checkout)
- `dev.ucp.shopping.ap2_mandates` (extends checkout — autonomous payment)
- `dev.ucp.shopping.buyer_consent`

---

## Transports

| Transport | Description | Use Case |
|-----------|-------------|----------|
| **REST** | OpenAPI over HTTPS | Standard HTTP clients |
| **MCP** | Model Context Protocol (JSON-RPC 2.0) | LLM tool calling (e.g., Claude, GPT) |
| **A2A** | Agent-to-Agent Protocol | Multi-agent pipelines |
| **Embedded** | OpenRPC for embedded UI widgets | Checkout embedded in platform UI |

A business may expose multiple transports for the same capability. The platform chooses based on its transport preference.

---

## Extensions

Extensions augment parent capabilities:

```json
{
  "name": "dev.ucp.shopping.fulfillment",
  "extends": [{ "name": "dev.ucp.shopping.checkout" }]
}
```

- Extensions use `allOf` with `$defs` keyed by parent capability name in JSON Schema
- Platform negotiates extensions separately from the parent capability
- A business that supports fulfillment lists it as a separate entry in `capabilities`

---

## UCP Envelope

Every UCP response includes a `ucp` envelope:

```json
{
  "ucp": {
    "version": "2026-04-08",
    "status": "success",
    "capabilities": { "dev.ucp.shopping.checkout": [{"version": "2026-04-08"}] },
    "payment_handlers": { "com.google.pay": [{"id": "gpay_1234", "version": "2026-04-08"}] }
  }
}
```

- `status`: `"success"` = response carries expected payload; `"error"` = response carries only `messages`
- `capabilities` and `payment_handlers` echo what was negotiated for this response

---

## Signals

Environment data provided by the platform for fraud/authorization — NOT buyer-asserted:

```json
{
  "signals": {
    "dev.ucp.buyer_ip": "203.0.113.42",
    "dev.ucp.user_agent": "Mozilla/5.0 ..."
  }
}
```

- Include in Create/Update/Complete Checkout where available
- Keys use reverse-domain naming
- Values MUST come from direct platform observation (IP, user agent, device fingerprint)
