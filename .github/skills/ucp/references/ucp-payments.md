# UCP Payments — Architecture and Payment Handlers

Spec: <https://ucp.dev/latest/specification/payment-handler-guide/>

---

## Overview

UCP decouples **payment method collection** (platform side) from **payment processing** (business side). This:
- Keeps platforms out of PCI-DSS scope for card data
- Allows businesses to stay Merchant of Record without sharing credentials with platforms
- Enables any payment handler (Google Pay, Apple Pay, Shop Pay, etc.) without platform-specific integrations

---

## Trust Triangle

```
  Platform ←──── discovers handlers from ────→ Business Profile
      │                                               │
      │  collects credential from buyer              │
      ↓                                               │
  Payment Handler (e.g., Google Pay)                 │
  returns token/credential ──────────────────────────→
      │                                               │
  Platform includes credential in complete_checkout ─→ Business processes payment
```

1. Business lists payment handlers in `/.well-known/ucp` under `payment_handlers`
2. Platform collects payment credentials from the buyer using the handler's SDK/UI
3. Platform includes collected credentials in `payment.instruments` on `complete_checkout`
4. Business receives the payment credential and processes it with their payment gateway

---

## Payment Handler Discovery

From the business profile:
```json
{
  "payment_handlers": {
    "com.google.pay": [
      {
        "id": "gpay_1234",
        "version": "2026-04-08",
        "config": {
          "environment": "TEST",
          "merchant_id": "merchant_12345",
          "merchant_name": "Example Shop"
        }
      }
    ],
    "com.apple.pay": [
      {
        "id": "applepay_5678",
        "version": "2026-04-08",
        "config": {
          "merchant_identifier": "merchant.com.example"
        }
      }
    ]
  }
}
```

Also available in checkout session responses (via `ucp.payment_handlers`) to reflect what's active for that session.

---

## Payment Instrument Structure

Populated by the platform when calling `complete_checkout`:

```json
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
          "card_art": "https://example.com/card-art.png",
          "description": "Google Pay •••• 5678"
        },
        "billing_address": {
          "street_address": "123 Main St",
          "address_country": "US",
          "postal_code": "12345"
        },
        "credential": {
          "type": "PAYMENT_GATEWAY",
          "token": "examplePaymentMethodToken"
        }
      }
    ]
  }
}
```

Key fields:
- `id`: unique ID assigned by platform for this instrument instance
- `handler_id`: matches the `id` from the payment handler definition in the profile
- `type`: broad category (`card`, `tokenized_card`, etc.) — handler-specific schemas constrain this
- `credential`: the payment credential — handler-specific format

---

## Credential Types

Payment handlers define their own credential schemas. Common patterns:

| Pattern | Description | Example handlers |
|---------|-------------|-----------------|
| **Processor tokenizer** | Handler generates a token redeemable by the business's payment processor | Most wallets (Google Pay, Apple Pay) |
| **Platform tokenizer** | Platform generates token, business receives raw credential | Platform-managed cards |
| **Encrypted credential** | Credential is encrypted for the business's processor | End-to-end encrypted card data |

See examples:
- Processor tokenizer: <https://ucp.dev/latest/specification/examples/processor-tokenizer-payment-handler/>
- Platform tokenizer: <https://ucp.dev/latest/specification/examples/platform-tokenizer-payment-handler/>
- Encrypted credential: <https://ucp.dev/latest/specification/examples/encrypted-credential-handler/>

---

## AP2 Mandates (Autonomous Checkout)

The AP2 Mandates extension (`dev.ucp.shopping.ap2_mandates`) enables fully autonomous checkout — no buyer handoff required:

- Platform signals it has a mandate from the buyer to complete checkout autonomously
- Business validates the mandate and allows direct `complete_checkout` without `continue_url` escalation
- Requires explicit buyer consent and mandate negotiation

Spec: <https://ucp.dev/latest/specification/ap2-mandates/>

---

## Checkout `payment` Field Lifecycle

- **Create Checkout**: `payment` is optional (omit for digital goods / quote generation)
- **Update Checkout**: include `payment.instruments` if you want to preview payment method
- **Complete Checkout**: include `payment.instruments` with selected instrument(s) and credentials

The `payment_handlers` registry in the checkout response updates dynamically as the business determines which handlers apply to the current session (e.g., based on currency, country, or cart contents).

---

## PCI-DSS Scope

- Platform: NOT in PCI scope — never receives raw card numbers (handlers tokenize before returning)
- Business: NOT in PCI scope — receives tokens, not raw card data
- Payment handler / processor: IN PCI scope — handles raw card data within their certified environment
