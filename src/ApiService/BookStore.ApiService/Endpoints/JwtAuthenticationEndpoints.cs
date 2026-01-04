using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.ApiService.Models;
using BookStore.ApiService.Services;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints;

/// <summary>
/// JWT-based authentication endpoints for login, register, and token refresh
/// </summary>
public static class JwtAuthenticationEndpoints
{
    public static RouteGroupBuilder MapJwtAuthenticationEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/login", LoginAsync)
            .WithName("JwtLogin")
            .WithSummary("Login with email and password to receive JWT tokens");

        _ = group.MapPost("/register", RegisterAsync)
            .WithName("JwtRegister")
            .WithSummary("Register a new user account");

        _ = group.MapPost("/confirmEmail", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirm a user's email address using a verification code");

        _ = group.MapPost("/refresh", RefreshTokenAsync)
            .WithName("JwtRefresh")
            .WithSummary("Refresh an expired access token using a refresh token");

        return group;
    }

    static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        ILogger<Program> logger)
    {
        logger.LogInformation("JWT login attempt for {Email}", request.Email);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            logger.LogWarning("Login failed: User not found for {Email}", request.Email);
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            logger.LogWarning("Login failed: Invalid password for {Email}", request.Email);
            return Results.Unauthorized();
        }

        // Build claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        // Add roles as claims
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessToken = jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Store refresh token
        var refreshTokenInfo = new RefreshTokenInfo(refreshToken, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);
        user.RefreshTokens.Add(refreshTokenInfo);

        // Prune old tokens (optional, keep latest 5)
        if (user.RefreshTokens.Count > 5)
        {
            user.RefreshTokens = [.. user.RefreshTokens.OrderByDescending(r => r.Created).Take(5)];
        }

        _ = await userManager.UpdateAsync(user);

        logger.LogInformation("JWT login successful for {Email}", request.Email);

        return Results.Ok(new LoginResponse(
            TokenType: "Bearer",
            AccessToken: accessToken,
            ExpiresIn: 3600,
            RefreshToken: refreshToken
        ));
    }

    static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        Wolverine.IMessageBus bus,
        IOptions<Infrastructure.Email.EmailOptions> emailOptions,
        ILogger<Program> logger)
    {
        logger.LogInformation("JWT registration attempt for {Email}", request.Email);

        var verificationRequired = emailOptions.Value.DeliveryMethod != "None";

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = !verificationRequired
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email}: {Errors}",
                request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return Results.BadRequest(new { errors = result.Errors });
        }

        if (verificationRequired)
        {
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(user.Id, user.Email!, code, user.UserName!));
        }

        // Build claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        var accessToken = jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Store refresh token
        var refreshTokenInfo = new RefreshTokenInfo(refreshToken, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow);
        user.RefreshTokens.Add(refreshTokenInfo);

        // Update user to persist token
        _ = await userManager.UpdateAsync(user);

        logger.LogInformation("JWT registration successful for {Email}", request.Email);

        return Results.Ok(new LoginResponse(
            TokenType: "Bearer",
            AccessToken: accessToken,
            ExpiresIn: 3600,
            RefreshToken: refreshToken
        ));
    }

    static async Task<IResult> ConfirmEmailAsync(
        string userId,
        string code,
        UserManager<ApplicationUser> userManager,
        Wolverine.IMessageBus bus,
        ILogger<Program> logger)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            return Results.NotFound("Invalid user ID or code.");
        }

        var result = await userManager.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
        {
            await bus.PublishAsync(new BookStore.ApiService.Events.Notifications.UserVerifiedNotification(user.Id, user.Email!, DateTimeOffset.UtcNow));
            return Results.Ok("Email confirmed successfully.");
        }

        return Results.BadRequest("Error confirming email.");
    }

    static async Task<IResult> RefreshTokenAsync(
        RefreshRequest request,
        JwtTokenService jwtTokenService,
        UserManager<ApplicationUser> userManager,
        Marten.IDocumentSession session,
        ILogger<Program> logger)
    {
        // 1. Find user with this refresh token
        // Since we store tokens in the user document, we need to query based on the token
        var user = await session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == request.RefreshToken));

        if (user == null)
        {
            logger.LogWarning("Refresh failed: Token not found");
            return Results.Unauthorized();
        }

        // 2. Validate token
        var existingToken = user.RefreshTokens.FirstOrDefault(rt => rt.Token == request.RefreshToken);
        if (existingToken == null || existingToken.Expires <= DateTimeOffset.UtcNow)
        {
            logger.LogWarning("Refresh failed: Token expired or invalid for user {User}", user.UserName);
            // Optionally remove expired token
            if (existingToken != null)
            {
                _ = user.RefreshTokens.Remove(existingToken);
                _ = await userManager.UpdateAsync(user);
            }

            return Results.Unauthorized();
        }

        // 3. Generate new tokens
        // Build claims again
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var newAccessToken = jwtTokenService.GenerateAccessToken(claims);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();

        // 4. Rotate refresh token (remove old, add new)
        _ = user.RefreshTokens.Remove(existingToken);
        user.RefreshTokens.Add(new RefreshTokenInfo(newRefreshToken, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow));

        // Prune old tokens
        if (user.RefreshTokens.Count > 5)
        {
            user.RefreshTokens = [.. user.RefreshTokens.OrderByDescending(r => r.Created).Take(5)];
        }

        _ = await userManager.UpdateAsync(user);

        return Results.Ok(new LoginResponse(
            TokenType: "Bearer",
            AccessToken: newAccessToken,
            ExpiresIn: 3600,
            RefreshToken: newRefreshToken
        ));
    }
}
