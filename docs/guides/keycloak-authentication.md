# Keycloak Authentication Guide

## Overview

BookStore authentication has been migrated from custom ASP.NET Core Identity + JWT token issuance to Keycloak-based OpenID Connect (OIDC) and JWT bearer validation.

### What changed

- Web app authentication now uses OIDC redirects with Keycloak.
- API token validation now trusts Keycloak-issued access tokens.
- Legacy `/account/*` authentication endpoints were removed from the application API surface.
- Email verification now uses Keycloak built-in verification flows.
- Passkey management tied to the previous Identity implementation was removed.

### High-level architecture

- BookStore.AppHost starts a Keycloak container and imports the `bookstore` realm.
- BookStore.Web performs login/logout with OIDC middleware and secure auth cookies.
- BookStore.Web forwards Keycloak access tokens when calling BookStore.ApiService.
- BookStore.ApiService validates Keycloak JWT access tokens and enforces policies.

## Architecture

### Components and responsibilities

- `src/BookStore.AppHost/`
  - Provisions Keycloak resource.
  - Imports realm configuration from `src/BookStore.AppHost/Realms/bookstore-realm.json`.
- `src/BookStore.Web/`
  - Uses `AddKeycloakOpenIdConnect` for login and token acquisition.
  - Exposes `/login/oidc` and `/logout` endpoints for challenge/sign-out flow.
  - Uses scoped token accessor to attach access tokens to Refit API clients.
- `src/BookStore.ApiService/`
  - Uses `AddKeycloakJwtBearer` for token validation.
  - Transforms Keycloak role claims for policy/role authorization.

### Request flow

1. User visits a protected UI and is redirected to `/login/oidc`.
2. OIDC challenge redirects to Keycloak login page.
3. Keycloak authenticates user and redirects back to the web callback.
4. Web app stores OIDC session in cookie and forwards `access_token` to API.
5. API validates token signature, issuer, audience, and tenant claims.

## Multi-tenancy With Single Realm

BookStore uses one Keycloak realm (`bookstore`) with a tenant discriminator (`tenant_id`) stored as a user attribute and emitted as a top-level claim.

### Why single realm

- Startup OIDC configuration stays static and simple.
- Tenant onboarding does not require realm creation.
- Existing middleware keeps using `tenant_id` claim enforcement.

### Claim mapping

- Keycloak protocol mapper maps user attribute `tenant_id` into token claim `tenant_id`.
- API middleware enforces claim and tenant context boundaries.

### Tenant selection behavior in Web

- When authenticated, Web initializes tenant from the `tenant_id` claim.
- Anonymous flows continue to support tenant selection via URL and local storage fallback.

## Email Verification Via Keycloak

Email verification is managed by Keycloak required actions.

### Behavior

- New users are prompted for verification according to realm settings.
- Keycloak sends verification emails using configured SMTP settings.
- Application-side email confirmation endpoints/pages are no longer part of auth.

### Local development

- Development users in realm import can be marked `emailVerified: true` for bootstrap convenience.

## Realm Configuration

Realm source file:

- `src/BookStore.AppHost/Realms/bookstore-realm.json`

### Required realm elements

- Realm: `bookstore`
- Clients:
  - `bookstore-api` for API audience validation
  - `bookstore-web` for OIDC browser login
- Roles: `Admin`, `User`
- Protocol mappers:
  - `tenant_id` user attribute -> token claim
  - realm roles mapping (and API-side claim transformation as needed)
- Password policy and verification settings
- Dev/test seed user(s)

## Local Development With Aspire

### Startup

- Run distributed app via `aspire run`.
- AppHost starts Keycloak, API, and Web with service discovery.

### Service discovery keys used by Web

- `services:keycloak:https:0`
- `services:keycloak:http:0`

Web uses these values for:

- OIDC integration (`AddKeycloakOpenIdConnect`)
- Registration redirect URL construction
- Account console redirect links

## Creating New Tenants

Tenant creation remains API-driven and now provisions Keycloak users for tenant administration.

### Flow

1. POST `/api/admin/tenants`
2. API creates tenant record.
3. API calls Keycloak Admin API to create tenant admin user with `tenant_id` attribute.
4. Admin role assignment is completed in Keycloak.

If Keycloak provisioning fails, tenant creation returns failure with ProblemDetails.

## User Management Via Keycloak Admin Console

User lifecycle (create/disable/reset credentials/role assignment) is managed in Keycloak Admin Console.

### Operational note

- Application admin-user endpoints are intentionally not the primary management path.
- Use Keycloak console for user administration workflows.

## Testing Authentication (ROPC)

Integration tests can use ROPC against Keycloak token endpoint.

### Token endpoint

- `/realms/bookstore/protocol/openid-connect/token`

### Typical test grant fields

- `grant_type=password`
- `client_id=bookstore-web`
- `username=<user>`
- `password=<password>`

### Important

ROPC is intended for development and automated tests only.

## Migration From Custom JWT

### Breaking changes

- Custom API account endpoints removed from active auth flow.
- Blazor pages for in-app registration/verify-email/passkeys removed or redirected.
- Token refresh/token parsing logic formerly in Web auth services removed.

### Existing data considerations

- Legacy user-store records are no longer the source of authentication truth.
- Authorization now depends on Keycloak claims (`sub`, roles, `tenant_id`).

## Production Deployment Notes

### Security hardening checklist

- Enforce HTTPS metadata (`RequireHttpsMetadata = true`).
- Disable ROPC direct access grants on browser clients unless explicitly required.
- Configure SMTP relay securely in Keycloak.
- Keep realm export files free of real credentials.
- Use managed secrets/configuration for Keycloak admin and client settings.

### Operational recommendations

- Use external, persistent Keycloak deployment for production.
- Monitor token validation failures and tenant-claim mismatches.
- Validate role mappings after realm changes.
