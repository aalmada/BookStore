using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.ApiService.Projections;
using BookStore.ApiService.Services;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Messages.Events;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        _ = group.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirm a user's email address using a verification code");

        _ = group.MapPost("/refresh-token", RefreshTokenAsync)
            .WithName("JwtRefresh")
            .WithSummary("Refresh an expired access token using a refresh token");

        _ = group.MapPost("/logout", LogoutAsync)
            .WithName("JwtLogout")
            .WithSummary("Logout and invalidate refresh token")
            .RequireAuthorization();

        _ = group.MapPost("/resend-verification", ResendVerificationAsync)
            .WithName("ResendVerification")
            .WithSummary("Resend the email verification link")
            .WithDescription("Resends the email verification link. Enforces a 60-second cooldown period. Returns 200 OK even if the email is already verified or does not exist to prevent enumeration.")
            .AllowAnonymous();

        _ = group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Change user password")
            .RequireAuthorization();

        _ = group.MapPost("/add-password", AddPasswordAsync)
            .WithName("AddPassword")
            .WithSummary("Set a password for a user without one")
            .RequireAuthorization();

        _ = group.MapGet("/password-status", GetPasswordStatusAsync)
            .WithName("GetPasswordStatus")
            .WithSummary("Check if the user has a password set")
            .RequireAuthorization();

        _ = group.WithMetadata(new AllowAnonymousTenantAttribute());
        return group.RequireRateLimiting("AuthPolicy");
    }

    /// <summary>
    /// Builds the standard set of claims for a user token
    /// </summary>
    static List<Claim> BuildUserClaims(ApplicationUser user, string tenantId, IEnumerable<string> roles)
    => [
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.UserName!),
        new(ClaimTypes.Email, user.Email!),
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email!),
        new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        new("tenant_id", tenantId),
        .. roles.Select(role => new Claim(ClaimTypes.Role, string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : role))
    ];

    static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        Log.Users.JwtLoginAttempt(logger, request.Email);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {

            Log.Users.LoginFailedUserNotFound(logger, request.Email);
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            Log.Users.AccountLocked(logger, request.Email);
            return Results.BadRequest(new { error = "Account is locked out." });
        }

        if (result.IsNotAllowed)
        {
            if (userManager.Options.SignIn.RequireConfirmedEmail && !await userManager.IsEmailConfirmedAsync(user))
            {
                Log.Users.LoginFailedUnconfirmedEmail(logger, request.Email);
                return Results.Json(new { error = "Requires verification", message = "Please confirm your email address." }, statusCode: 401);
            }

            Log.Users.LoginFailedUserNotFound(logger, request.Email); // Or a generic "Not allowed" log
            return Results.Unauthorized();
        }

        if (!result.Succeeded)
        {
            Log.Users.LoginFailedInvalidPassword(logger, request.Email);
            return Results.Unauthorized();
        }

        // Build claims and generate tokens
        var roles = await userManager.GetRolesAsync(user);
        var claims = BuildUserClaims(user, tenantContext.TenantId, roles);
        var accessToken = jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Store refresh token with tenant context for security validation
        user.RefreshTokens.Add(new RefreshTokenInfo(
            refreshToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            tenantContext.TenantId));

        // Prune old tokens (keep latest 5)
        if (user.RefreshTokens.Count > 5)
        {
            user.RefreshTokens = [.. user.RefreshTokens.OrderByDescending(r => r.Created).Take(5)];
        }

        _ = await userManager.UpdateAsync(user);

        Log.Users.JwtLoginSuccessful(logger, request.Email);

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
        ITenantContext tenantContext,
        [FromServices] IDocumentSession session,
        Wolverine.IMessageBus bus,
        IOptions<Infrastructure.Email.EmailOptions> emailOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        Log.Users.JwtRegistrationAttempt(logger, request.Email);

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
            // Security: Mask "User already exists" errors to prevent enumeration
            if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                Log.Users.RegistrationFailed(logger, request.Email, "User already exists (masked)");

                // Return same success message as normal registration
                return Results.Ok(new { message = "Registration successful. Please check your email to verify your account." });
            }

            Log.Users.RegistrationFailed(logger, request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return Results.BadRequest(new { errors = result.Errors });
        }

        if (verificationRequired)
        {
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(user.Id, user.Email!, code, user.UserName!));

            return Results.Ok(new { message = "Registration successful. Please check your email to verify your account." });
        }

        // Build claims and generate tokens
        var claims = BuildUserClaims(user, tenantContext.TenantId, []);
        var accessToken = jwtTokenService.GenerateAccessToken(claims);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Store refresh token with tenant context
        user.RefreshTokens.Add(new RefreshTokenInfo(
            refreshToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            tenantContext.TenantId));

        _ = await userManager.UpdateAsync(user);

        // Emit UserProfileCreated event to initialize the projection (favorites, cart, etc.)
        _ = session.Events.StartStream<UserProfile>(user.Id, new UserProfileCreated(user.Id));
        await session.SaveChangesAsync(cancellationToken);

        Log.Users.JwtRegistrationSuccessful(logger, request.Email);

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
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest("Error confirming email.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            Log.Users.ConfirmationFailedUserNotFound(logger, userId);
            // Don't reveal that the user does not exist
            return Results.BadRequest("Error confirming email.");
        }

        var result = await userManager.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
        {
            await bus.PublishAsync(new BookStore.Shared.Notifications.UserVerifiedNotification(Guid.Empty, user.Id, user.Email!, DateTimeOffset.UtcNow));
            return Results.Ok("Email confirmed successfully.");
        }

        Log.Users.ConfirmationFailedInvalidCode(logger, user.Id.ToString(), string.Join(", ", result.Errors.Select(e => e.Description)));
        return Results.BadRequest("Error confirming email.");
    }

    static async Task<IResult> ResendVerificationAsync(
        [FromBody] ResendVerificationRequest request,
        UserManager<ApplicationUser> userManager,
        Wolverine.IMessageBus bus,
        IOptions<Infrastructure.Email.EmailOptions> emailOptions,
        IWebHostEnvironment env,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest("Email is required.");
        }

        Log.Users.ResendVerificationAttempt(logger, request.Email);

        if (emailOptions.Value.DeliveryMethod == "None" && !env.IsDevelopment())
        {
            return Results.BadRequest("Email verification is currently disabled.");
        }

        // Generic message to prevent email enumeration
        var genericMessage = new { message = "If an account exists for this email, a new verification link has been sent." };

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // For security, don't reveal if user exists
            Log.Users.ResendVerificationFailed(logger, request.Email, "User not found");
            return Results.Ok(genericMessage);
        }

        if (user.EmailConfirmed)
        {
            Log.Users.ResendVerificationFailed(logger, request.Email, "Email already confirmed");
            // Return success to prevent enumeration
            return Results.Ok(genericMessage);
        }

        // Rate limiting: Check if we sent an email recently (e.g., last 60 seconds)
        if (user.LastVerificationEmailSent.HasValue &&
            DateTimeOffset.UtcNow - user.LastVerificationEmailSent.Value < TimeSpan.FromSeconds(60))
        {
            Log.Users.ResendVerificationFailed(logger, request.Email, "Rate limit exceeded (cooldown)");
            // Return success to prevent timing attacks or revealing state
            return Results.Ok(genericMessage);
        }

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(user.Id, user.Email!, code, user.UserName!));

        // Update the timestamp
        user.LastVerificationEmailSent = DateTimeOffset.UtcNow;
        _ = await userManager.UpdateAsync(user);

        Log.Users.ResendVerificationSuccessful(logger, request.Email);

        return Results.Ok(genericMessage);
    }

    static async Task<IResult> RefreshTokenAsync(
        RefreshRequest request,
        JwtTokenService jwtTokenService,
        UserManager<ApplicationUser> userManager,
        Marten.IDocumentSession session,
        ITenantContext tenantContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // 1. Find user with this refresh token
        // Since we store tokens in the user document, we need to query based on the token
        var user = await session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == request.RefreshToken), cancellationToken);

        if (user == null)
        {
            Log.Users.RefreshFailedTokenNotFound(logger);
            return Results.Unauthorized();
        }

        // 2. Validate token exists and is not expired
        var existingToken = user.RefreshTokens.FirstOrDefault(rt => rt.Token == request.RefreshToken);
        if (existingToken == null || existingToken.Expires <= DateTimeOffset.UtcNow)
        {
            Log.Users.RefreshFailedTokenExpiredOrInvalid(logger, user.UserName);
            if (existingToken != null)
            {
                _ = user.RefreshTokens.Remove(existingToken);
                _ = await userManager.UpdateAsync(user);
            }

            return Results.Unauthorized();
        }

        // 3. Validate tenant matches the token's original tenant (security: prevent cross-tenant token theft)
        if (!string.Equals(existingToken.TenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            Log.Users.RefreshFailedTokenExpiredOrInvalid(logger, user.UserName);
            return Results.Unauthorized();
        }

        // 4. Generate new tokens using the original tenant from the refresh token
        var roles = await userManager.GetRolesAsync(user);
        var claims = BuildUserClaims(user, existingToken.TenantId, roles);
        var newAccessToken = jwtTokenService.GenerateAccessToken(claims);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();

        // 5. Rotate refresh token (remove old, add new)
        _ = user.RefreshTokens.Remove(existingToken);
        user.RefreshTokens.Add(new RefreshTokenInfo(
            newRefreshToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            existingToken.TenantId));

        // Prune old tokens (keep latest 5)
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

    static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Results.Unauthorized();
        }

        var appUser = await userManager.FindByIdAsync(userId);
        if (appUser == null)
        {
            return Results.Unauthorized();
        }

        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            // Remove specific refresh token
            var tokenToRemove = appUser.RefreshTokens.FirstOrDefault(rt => rt.Token == request.RefreshToken);
            if (tokenToRemove != null)
            {
                _ = appUser.RefreshTokens.Remove(tokenToRemove);
                Log.Users.LogoutSuccessful(logger, appUser.UserName);
            }
        }
        else
        {
            // Remove all refresh tokens for this user (full logout)
            appUser.RefreshTokens.Clear();
            Log.Users.LogoutSuccessful(logger, appUser.UserName);
        }

        _ = await userManager.UpdateAsync(appUser);

        return Results.Ok();
    }

    static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser == null)
        {
            return Results.Unauthorized();
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return Results.BadRequest(new { errors = new[] { new { code = "PasswordReuse", description = "New password cannot be the same as the current password." } } });
        }

        var result = await userManager.ChangePasswordAsync(appUser, request.CurrentPassword, request.NewPassword);
        if (result.Succeeded)
        {
            return Results.Ok(new { message = "Password changed successfully." });
        }

        return Results.BadRequest(new { errors = result.Errors });
    }

    static async Task<IResult> AddPasswordAsync(
        [FromBody] AddPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser == null)
        {
            return Results.Unauthorized();
        }

        var result = await userManager.AddPasswordAsync(appUser, request.NewPassword);
        if (result.Succeeded)
        {
            return Results.Ok(new { message = "Password set successfully." });
        }

        return Results.BadRequest(new { errors = result.Errors });
    }

    static async Task<IResult> GetPasswordStatusAsync(
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal user)
    {
        var appUser = await userManager.GetUserAsync(user);
        if (appUser == null)
        {
            return Results.Unauthorized();
        }

        var hasPassword = await userManager.HasPasswordAsync(appUser);
        return Results.Ok(new PasswordStatusResponse(hasPassword));
    }
}
