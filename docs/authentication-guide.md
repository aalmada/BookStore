# Authentication Guide

This guide covers the authentication and authorization system in the BookStore application. The system uses a **JWT-based architecture** where the Blazor Server frontend acts as a client to the backend API, storing tokens in memory for security.

## Architecture

The system uses a pure **Token-Based** approach to unify the auth model for web, mobile, and third-party clients.

### Authentication Flow

```mermaid
graph TB
    subgraph "Blazor Frontend"
        A[User Login] --> B[AuthenticationService]
        B --> C[TokenService (In-Memory)]
        C --> D[JwtAuthenticationStateProvider]
        D --> E[Notify State Changed]
    end
    
    subgraph "Backend API"
        F[Identity Endpoints] --> G[JwtTokenService]
        G --> H[Issue Access/Refresh Tokens]
        I[API Requests] --> J[Authorization Header]
        J --> K[Validate Bearer Token]
    end
    
    B --> F
```

### Key Components

#### Frontend (Blazor Server)
- **`AuthenticationService`**: High-level service for Login, Register, and Logout operations.
- **`TokenService`**: Stores Access and Refresh tokens in **Scoped Memory** (per SignalR circuit).
    - *Security Note*: Tokens are **NOT** stored in LocalStorage or Cookies to prevent XSS attacks.
    - Tokens persist only for the lifetime of the user's session (browser tab).
- **`JwtAuthenticationStateProvider`**: Custom provider that:
    - Reads tokens from `TokenService`.
    - Parses JWT claims to set the user's `AuthenticationState`.
    - Automatically handles **Silent Refresh** when the access token is close to expiry.

#### Backend (API)
- **`JwtTokenService`**: Responsible for generating signed JWTs (Access Tokens) and secure Refresh Tokens.
- **`JwtAuthenticationEndpoints`**:
    - `POST /identity/login`: Exchange credentials for tokens.
    - `POST /identity/refresh`: Exchange refresh token for new access token.
- **Passkey Integration**: Passkey login flow (`/account/assertion/result`) also results in the issuance of standard JWTs, making the frontend agnostic to *how* the user logged in.

## Authentication Methods

### 1. Password Authentication

Standard email/password login flow.

**Endpoint**: `POST /identity/login`
**Request**: `{ "email": "...", "password": "..." }`
**Response**:
```json
{
  "accessToken": "ey...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

### 2. Passkey Authentication (Passwordless)

The application supports WebAuthn/FIDO2 for passwordless login. This flow is fully integrated with the JWT system.

**Flow**:
1.  Frontend gets assertion options (`/account/assertion/options`).
2.  User authenticates with FaceID/TouchID.
3.  Frontend sends assertion to `/account/assertion/result`.
4.  **Backend issues JWT tokens** just like a password login.

See [Passkey Guide](passkey-guide.md) for implementation details.

## State Management & Re-Authentication

### In-Memory Token Storage
We store tokens in a Scoped service (`TokenService`). This means:
- **Pros**: Immune to XSS (malicious JS cannot read the service memory).
- **Cons**: User is logged out if they refresh the page (F5).

### Preventing Logout on Refresh
To mitigate the refresh issue while maintaining security, the application can optionally use a **Refresh Token** flow that persists *only* the refresh token in a secure HttpOnly cookie (future enhancement) or relies on the user simply logging in again, which is acceptable for high-security banking-style apps. 

*Current Implementation*: In-memory only. Refreshing the page requires re-login.

### Authorization Headers
All outgoing HTTP requests to the API are intercepted by `AuthorizationMessageHandler`, which attaches the Bearer token:

```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    var token = await _tokenService.GetAccessTokenAsync();
    if (!string.IsNullOrEmpty(token))
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    return await base.SendAsync(request, cancellationToken);
}
```

## Authorization

Role-based authorization is enforced via standard ASP.NET Core policies.

### Roles
- **Admin**: Full access to management endpoints.
- **User**: Standard access (can manage own profile/orders).

### Backend Enforcement
Endpoints are protected using the `[Authorize]` attribute or `.RequireAuthorization()` extension method.

```csharp
app.MapPost("/api/admin/books", ...)
   .RequireAuthorization("Admin");
```

## User Model (`ApplicationUser`)

Users are stored in **Marten** (PostgreSQL) as JSON documents.

```csharp
public class ApplicationUser
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public List<string> Roles { get; set; }
    // ...
}
```

## Rate Limiting

To protect against abuse and Denial of Service (DoS) attacks, all authentication endpoints are protected by the **AuthPolicy**.

- **Limit**: Configurable via `RateLimit:PermitLimit`.
    - **Production**: Defaults to 10 requests per minute.
    - **Development**: Defaults to 1000 requests per minute (to support **parallel integration tests**).
- **Scope**: Applied globally to all endpoint in the `/account` group (Login, Register, Passkeys, etc.).
- **Response**: `429 Too Many Requests` when exceeded.

## Related Guides

- [Passkey Guide](passkey-guide.md)
- [API Conventions](api-conventions-guide.md)
- [Marten Guide](marten-guide.md)
