using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.ApiService.Models;
using BookStore.ApiService.Services;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Identity;

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

        logger.LogInformation("JWT login successful for {Email}", request.Email);

        // TODO: Store refresh token in database for validation

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
        ILogger<Program> logger)
    {
        logger.LogInformation("JWT registration attempt for {Email}", request.Email);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            logger.LogWarning("Registration failed for {Email}: {Errors}",
                request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return Results.BadRequest(new { errors = result.Errors });
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

        logger.LogInformation("JWT registration successful for {Email}", request.Email);

        return Results.Ok(new LoginResponse(
            TokenType: "Bearer",
            AccessToken: accessToken,
            ExpiresIn: 3600,
            RefreshToken: refreshToken
        ));
    }

    static Task<IResult> RefreshTokenAsync(
        RefreshRequest request,
        JwtTokenService jwtTokenService,
        ILogger<Program> logger)
    {
        logger.LogWarning("Refresh token endpoint called but not yet implemented");

        // TODO: Implement refresh token validation
        // 1. Validate refresh token from database
        // 2. Check if token is expired
        // 3. Generate new access token
        // 4. Optionally rotate refresh token

        return Task.FromResult(Results.Problem(
            title: "Not Implemented",
            detail: "Refresh token functionality is not yet implemented",
            statusCode: StatusCodes.Status501NotImplemented
        ));
    }
}
