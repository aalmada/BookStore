# Authentication Guide

This guide covers user authentication and authorization in the BookStore API using ASP.NET Core Identity with JWT bearer tokens.

## Overview

The BookStore API implements a modern authentication system with:

- **JWT Bearer Tokens** for stateless API authentication
- **Role-Based Authorization** for admin endpoints
- **Marten Integration** for user storage (no Entity Framework Core)
- **Standard Identity Endpoints** for registration, login, and account management
- **Extensible Design** ready for Passkey/WebAuthn support

## Architecture

### Components

```mermaid
graph LR
    A[Client] -->|JWT Token| B[API Gateway]
    B --> C[Authentication Middleware]
    C --> D[Authorization Middleware]
    D --> E[Endpoints]
    E --> F[MartenUserStore]
    F --> G[PostgreSQL]
```

### ApplicationUser Model

Users are stored as documents in Marten with the following properties:

```csharp
public sealed class ApplicationUser
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public ICollection<string> Roles { get; set; }
    // ... additional Identity properties
}
```

### MartenUserStore

Custom implementation of ASP.NET Core Identity stores:
- `IUserStore<ApplicationUser>` - User CRUD operations
- `IUserPasswordStore<ApplicationUser>` - Password management
- `IUserEmailStore<ApplicationUser>` - Email management
- `IUserRoleStore<ApplicationUser>` - Role assignment

## Identity Endpoints

All authentication endpoints are available under the `/identity` route group:

### Registration

**POST** `/identity/register`

Register a new user account.

```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response**: `200 OK` on success

### Login

**POST** `/identity/login`

Authenticate and receive JWT tokens.

```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response**:
```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGc...",
  "expiresIn": 3600,
  "refreshToken": "CfDJ8..."
}
```

### Token Refresh

**POST** `/identity/refresh`

Refresh an expired access token.

```json
{
  "refreshToken": "CfDJ8..."
}
```

### Account Management

- **GET** `/identity/manage/info` - Get user information
- **POST** `/identity/manage/info` - Update user information
- **POST** `/identity/forgotPassword` - Request password reset
- **POST** `/identity/resetPassword` - Reset password
- **POST** `/identity/resendConfirmationEmail` - Resend email confirmation

## Authorization

### Role-Based Authorization

The API uses role-based authorization to protect admin endpoints.

#### Available Roles

- **Admin** - Full access to all admin endpoints

#### Protecting Endpoints

Admin endpoints require the `Admin` role:

```csharp
public static RouteGroupBuilder MapAdminBookEndpoints(this RouteGroupBuilder group)
{
    // ... endpoint mappings
    return group.RequireAuthorization("Admin");
}
```

### Using JWT Tokens

Include the access token in the `Authorization` header:

```bash
curl -X GET http://localhost:5000/admin/books \
  -H "Authorization: Bearer eyJhbGc..."
```

### Authorization Responses

- **401 Unauthorized** - No token provided or invalid token
- **403 Forbidden** - Valid token but insufficient permissions

## Development Setup

### Default Admin User

In development, a default admin user is automatically seeded:

> [!WARNING]
> **Development Only** - Change these credentials in production!

- **Email**: `admin@bookstore.com`
- **Password**: `Admin123!`
- **Role**: `Admin`

### Testing Authentication

#### 1. Login as Admin

```bash
curl -X POST http://localhost:5000/identity/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@bookstore.com",
    "password": "Admin123!"
  }'
```

Save the `accessToken` from the response.

#### 2. Access Admin Endpoint

```bash
curl -X GET http://localhost:5000/admin/books \
  -H "Authorization: Bearer <access_token>"
```

#### 3. Register New User

```bash
curl -X POST http://localhost:5000/identity/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@example.com",
    "password": "User123!"
  }'
```

## Configuration

### JWT Settings

JWT authentication is configured in [ApplicationServicesExtensions.cs](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Infrastructure/Extensions/ApplicationServicesExtensions.cs#L87-L102):

```csharp
services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddUserStore<MartenUserStore>();

services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);
```

### Authorization Policies

The "Admin" policy is defined in the same file:

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
```

## Production Considerations

### Security Best Practices

> [!IMPORTANT]
> Follow these security guidelines in production:

1. **Strong Passwords** - Enforce password complexity requirements
2. **HTTPS Only** - Never send tokens over HTTP
3. **Token Expiration** - Use short-lived access tokens (default: 1 hour)
4. **Refresh Tokens** - Implement secure refresh token rotation
5. **Rate Limiting** - Protect login endpoints from brute force attacks

### Environment-Specific Configuration

```csharp
if (app.Environment.IsDevelopment())
{
    // Seed admin user only in development
    await DatabaseSeeder.SeedAdminUserAsync(userManager);
}
```

### User Management

To create admin users in production:

```csharp
var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

var adminUser = new ApplicationUser
{
    UserName = "admin@production.com",
    Email = "admin@production.com",
    EmailConfirmed = true
};

await userManager.CreateAsync(adminUser, "SecurePassword123!");
await userManager.AddToRoleAsync(adminUser, "Admin");
```

## Future Enhancements

### Passkey/WebAuthn Support

.NET 10+ supports Passkey authentication. To add this:

1. Implement `IUserTwoFactorStore<ApplicationUser>`
2. Add WebAuthn authentication scheme
3. Configure FIDO2 options

```csharp
services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme)
    .AddWebAuthn(); // Future enhancement
```

### Additional Roles

Add more granular roles:

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"))
    .AddPolicy("Editor", policy => policy.RequireRole("Admin", "Editor"))
    .AddPolicy("Viewer", policy => policy.RequireRole("Admin", "Editor", "Viewer"));
```

### Claims-Based Authorization

For fine-grained permissions:

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("CanEditBooks", policy => 
        policy.RequireClaim("Permission", "Books.Edit"));
```

## Troubleshooting

### Common Issues

#### 401 Unauthorized

**Cause**: Missing or invalid token

**Solution**: Ensure the `Authorization` header is set correctly:
```
Authorization: Bearer <access_token>
```

#### 403 Forbidden

**Cause**: Valid token but insufficient permissions

**Solution**: Verify the user has the required role:
```bash
# Check user roles via /identity/manage/info
curl -X GET http://localhost:5000/identity/manage/info \
  -H "Authorization: Bearer <access_token>"
```

#### Token Expired

**Cause**: Access token has expired (default: 1 hour)

**Solution**: Use the refresh token to get a new access token:
```bash
curl -X POST http://localhost:5000/identity/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "<refresh_token>"}'
```

## Related Guides

- [API Conventions](api-conventions-guide.md) - API design patterns
- [Marten Guide](marten-guide.md) - Document storage
- [Testing Guide](testing-guide.md) - Testing authentication
- [Configuration Guide](configuration-guide.md) - Configuration options
