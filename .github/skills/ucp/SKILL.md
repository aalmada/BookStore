---
name: ucp
description: Implement, review, and work with Universal Commerce Protocol (UCP) for e-commerce platforms and businesses — covering profile discovery (/.well-known/ucp), capability negotiation, checkout sessions, cart, catalog, orders, payment handlers, authentication (HTTP Message Signatures, OAuth, API keys), and transports (REST, MCP, A2A, Embedded). Trigger whenever the user mentions UCP, universal commerce protocol, checkout sessions, UCP profiles, /.well-known/ucp, capability negotiation, UCP-Agent header, payment handlers in commerce APIs, AI shopping agents, or any e-commerce platform/business protocol interoperability — even if they don't explicitly say 'UCP'. Always use this skill when implementing UCP-compliant APIs, building AI commerce agents, or integrating with UCP-enabled businesses.
---

# Universal Commerce Protocol (UCP) Skill

UCP is an open standard enabling **platforms** (AI agents, apps) and **businesses** (merchants) to interoperate on commerce operations—checkout, cart, catalog, orders, and payments—without bespoke integrations.

Current version: `2026-04-08` · Spec: <https://ucp.dev/latest/specification/overview/>

## Reference Index

| File | Topics |
|------|--------|
| [ucp-overview.md](references/ucp-overview.md) | Profiles, discovery, negotiation, namespace governance, versioning, transports, extensions |
| [ucp-checkout.md](references/ucp-checkout.md) | Checkout capability, status lifecycle, operations, error handling, totals, continue URL |
| [ucp-checkout-rest.md](references/ucp-checkout-rest.md) | REST binding—endpoints, headers, HTTP status codes, signing, complete examples |
| [ucp-payments.md](references/ucp-payments.md) | Payment architecture, handler discovery, trust model, PCI scope, credential types |
| [ucp-auth.md](references/ucp-auth.md) | Authentication options, HTTP Message Signatures (RFC 9421), key discovery, profile hosting |

## Core Concepts (Quick Orientation)

| Term | Meaning |
|------|---------|
| **Business** | Merchant/seller serving at `/.well-known/ucp` |
| **Platform** | AI agent, app, or aggregator sending `UCP-Agent` header |
| **Capability** | Named feature in reverse-domain format, e.g. `dev.ucp.shopping.checkout` |
| **Extension** | Capability that augments another via `extends` field |
| **Negotiation** | Intersection of supported capabilities/versions; both parties must agree |
| **Transport** | How capabilities are exposed: REST, MCP (JSON-RPC), A2A, Embedded |

## Roles You Might Implement

**Business (server side)**
- Host `/.well-known/ucp` profile listing capabilities, services, and payment handlers
- Implement capability endpoints (checkout, cart, catalog, order)
- Return `status`, `messages`, `continue_url`, and `totals` correctly
- Sign responses to `complete_checkout` with HTTP Message Signatures

**Platform (client side)**
- Fetch and cache business profile; negotiate capabilities
- Send `UCP-Agent: profile="https://platform.example/.well-known/ucp"` on every request
- Drive checkout state machine: create → update (buyer/fulfillment/payment) → complete
- Process `messages` array using the error priority algorithm
- Hand off to `continue_url` when `status` = `requires_escalation`

## Quick Reference

```
# Profile discovery
GET https://business.example.com/.well-known/ucp

# Checkout (REST)
POST   /checkout-sessions              # Create
GET    /checkout-sessions/{id}         # Get
PUT    /checkout-sessions/{id}         # Update (full replacement)
POST   /checkout-sessions/{id}/complete
POST   /checkout-sessions/{id}/cancel

# Required request header
UCP-Agent: profile="https://platform.example/.well-known/ucp"
```

## Error Processing Algorithm (Checkout)

```
1. Filter messages WHERE type = "error"
2. IF unrecoverable → retry with new resource or hand off via continue_url
3. IF recoverable   → fix inputs and call Update Checkout
4. IF requires_buyer_input / requires_buyer_review → hand off via continue_url
```

## Key Rules

- Capabilities use reverse-domain names: `com.example.my_feature`
- Versions are date strings: `YYYY-MM-DD`
- Update Checkout is a **full replacement** (PUT) — resend all fields you want to keep
- Totals MUST be rendered in order provided; do NOT recompute or reorder
- `continue_url` MUST be provided when `status` = `requires_escalation`
- `signals` values (IP, user agent) MUST NOT be buyer-asserted — platforms observe them directly
- Payment handlers are discovered from the business profile, not hard-coded

## Capabilities and Their Spec Pages

| Capability | Spec URL |
|-----------|----------|
| Checkout | <https://ucp.dev/latest/specification/checkout/> |
| Cart | <https://ucp.dev/latest/specification/cart/> |
| Catalog | <https://ucp.dev/latest/specification/catalog/> |
| Order | <https://ucp.dev/latest/specification/order/> |
| Identity Linking | <https://ucp.dev/latest/specification/identity-linking/> |
| AP2 Mandates | <https://ucp.dev/latest/specification/ap2-mandates/> |
| Fulfillment (extension) | <https://ucp.dev/latest/specification/fulfillment/> |
| Discount (extension) | <https://ucp.dev/latest/specification/discount/> |
| Buyer Consent (extension) | <https://ucp.dev/latest/specification/buyer-consent/> |
