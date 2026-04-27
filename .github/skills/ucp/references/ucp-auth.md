# UCP Authentication and Signatures

Spec: <https://ucp.dev/latest/specification/signatures/>  
Auth overview: <https://ucp.dev/latest/specification/overview/#security>

---

## Authentication Mechanisms

Authentication is optional and business-determined. Businesses may require it for some operations, leave others public.

| Mechanism | How | When to Use |
|-----------|-----|-------------|
| **API Key** | `X-API-Key: <key>` header | Simple business-to-platform keys |
| **OAuth 2.0** | `Authorization: Bearer <token>` (RFC 6749) | Platform auth on behalf of user (auth code) or platform-only (client credentials) |
| **Mutual TLS (mTLS)** | TLS client certificate | High-security environments |
| **HTTP Message Signatures** | `Signature` + `Signature-Input` headers (RFC 9421) | Cryptographic proof of platform identity; recommended for production |

---

## UCP-Agent Header (Identity)

Every request from a platform MUST include the `UCP-Agent` header to identify itself:

```http
UCP-Agent: profile="https://platform.example/.well-known/ucp"
```

- Uses RFC 8941 Dictionary Structured Field syntax
- The URL MUST point to the platform's UCP profile at `/.well-known/ucp`
- Businesses MAY fetch this profile to verify platform identity, capabilities, and keys

---

## HTTP Message Signatures (RFC 9421)

### Signing a Request

Required components to sign:
- `@method`
- `@authority` (host)
- `@path`
- `content-digest` (when body present)
- `content-type` (when body present)
- `idempotency-key` (when present)

1. Compute `Content-Digest`:
   ```
   Content-Digest: sha-256=:<base64-sha256-of-body>:
   ```

2. Build `Signature-Input`:
   ```
   Signature-Input: sig1=("@method" "@authority" "@path" "content-digest" "content-type");created=<unix-timestamp>;keyid="<key-id>"
   ```

3. Compute and add `Signature`:
   ```
   Signature: sig1=:<base64-signature>:
   ```

Full example:
```http
POST /checkout-sessions HTTP/1.1
Host: merchant.example.com
Content-Type: application/json
UCP-Agent: profile="https://platform.example/.well-known/ucp"
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Digest: sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:
Signature-Input: sig1=("@method" "@authority" "@path" "idempotency-key" "content-digest" "content-type");created=1744000000;keyid="platform-key-2025"
Signature: sig1=:MEUCIQDTxNq8h7LGHpvVZQp1iHkFp9+3N8Mxk2zH1wK4YuVN8w...:

{"line_items":[{"item":{"id":"item_123"},"quantity":2}]}
```

### Signing a Response

Businesses MUST sign `complete_checkout` responses; optional for others.

```http
HTTP/1.1 200 OK
Content-Type: application/json
Content-Digest: sha-256=:Y5fK8nLmPqRsT3vWxYzAbCdEfGhIjKlMnO...:
Signature-Input: sig1=("@status" "content-digest" "content-type");created=1744000001;keyid="merchant-key-2025"
Signature: sig1=:MFQCIH7kL9nM2oP5qR8sT1uV4wX6yZaB3cD...:

{"id":"chk_123","status":"completed","order":{"id":"ord_456","permalink_url":"..."}}
```

---

## Key Discovery

Public keys for verifying signatures are published in the profile at `/.well-known/ucp`:

```json
{
  "ucp": { "version": "2026-04-08" },
  "keys": [
    {
      "id": "merchant-key-2025",
      "algorithm": "ES256",
      "public_key": "<base64-encoded-public-key>"
    }
  ]
}
```

- Platforms look up the `keyid` from `Signature-Input` in the business's profile `keys` array
- Businesses look up the platform's key from the platform's profile (fetched via `UCP-Agent` URL)
- Cache profiles and keys; re-fetch on key-not-found

---

## Transport Security

- All UCP endpoints MUST be served over HTTPS with **TLS 1.3 minimum**
- HTTP Message Signatures provide application-level proof of origin (not just transport security)

---

## Profile Hosting Requirements

Businesses hosting a profile at `/.well-known/ucp`:
- MUST serve JSON with `Content-Type: application/json`
- MUST be accessible over HTTPS
- SHOULD include CORS headers if accessed from browser contexts
- SHOULD include `Cache-Control` headers for profile caching

Platforms hosting a profile at `/.well-known/ucp`:
- Similar requirements
- Profile MUST list capabilities the platform supports
- Profile SHOULD include public keys for signature verification

---

## OAuth Scopes and Flows

When using OAuth 2.0:

| Flow | Use Case | `Authorization` Header |
|------|----------|----------------------|
| Client Credentials | Platform authenticating as itself (no buyer) | `Bearer <platform_token>` |
| Authorization Code | Platform acting on behalf of authenticated buyer | `Bearer <user_token>` |

Businesses define which operations require which flows. Checkout-as-guest typically uses client credentials or no auth; authenticated buyer flows use authorization code.
