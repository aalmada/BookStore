using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace BookStore.ApiService.Endpoints;

public static class PasskeyEndpoints
{
    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var paramsGroup = endpoints.MapGroup("/account").RequireRateLimiting("AuthPolicy");
        _ = paramsGroup.WithMetadata(new AllowAnonymousTenantAttribute());

        // 1. Get Creation Options (Authenticated & Unauthenticated)
        _ = paramsGroup.MapPost("/attestation/options", async (
            HttpContext context,
            [FromBody] PasskeyCreationRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(context.User);

            // If authenticated, use current user (Add Passkey logic)
            if (user is not null)
            {
                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";
                var userEntity = new PasskeyUserEntity { Id = userId, Name = userName, DisplayName = userName };

                var creationOptions = await signInManager.MakePasskeyCreationOptionsAsync(userEntity);

                // Return consistent response format (same as unauthenticated flow)
                var response = new
                {
                    options = System.Text.Json.JsonDocument.Parse(creationOptions.ToString()).RootElement,
                    userId
                };

                return Results.Ok(response);
            }

            // If anonymous, we are in "Register new Account" flow
            if (string.IsNullOrEmpty(request.Email))
            {
                return Results.BadRequest("Email is required for new registration.");
            }

            var conflictingUser = await userManager.FindByEmailAsync(request.Email);
            if (conflictingUser is not null)
            {
                return Results.BadRequest("User already exists.");
            }

            // Create a temporary user entity for the purpose of generating options
            // The Client will sign this.
            var newUserId = Guid.CreateVersion7().ToString();
            var newUserEntity = new PasskeyUserEntity
            {
                Id = newUserId,
                Name = request.Email,
                DisplayName = request.Email
            };

            var newOptions = await signInManager.MakePasskeyCreationOptionsAsync(newUserEntity);

            // Return both the options AND the user ID so the client can send it back
            var newResponse = new
            {
                options = System.Text.Json.JsonDocument.Parse(newOptions.ToString()).RootElement,
                userId = newUserId
            };

            return Results.Ok(newResponse);
        });

        // 2. Register Passkey (Finish Registration)
        _ = paramsGroup.MapPost("/attestation/result", async (
            HttpContext context,
            [FromBody] RegisterPasskeyRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore,
            ITenantContext tenantContext,
            BookStore.ApiService.Services.JwtTokenService tokenService,
            Wolverine.IMessageBus bus,
            ILogger<Program> logger,
            Microsoft.Extensions.Options.IOptions<Infrastructure.Email.EmailOptions> emailOptions,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            var verificationRequired = emailOptions.Value.DeliveryMethod != "None";

            // Case A: Add to existing account
            if (user is not null)
            {
                var attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
                if (!attestation.Succeeded)
                {
                    return Results.BadRequest($"Attestation failed: {attestation.Failure?.Message}");
                }

                if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                {
                    await passkeyStore.AddOrUpdatePasskeyAsync(user, attestation.Passkey, cancellationToken);
                    // Persist the changes to the database
                    _ = await userManager.UpdateAsync(user);
                }

                return Results.Ok(new { Message = "Passkey added." });
            }

            // Case B: Sign Up new account
            // We need the email to create the user.
            if (string.IsNullOrEmpty(request.Email))
            {
                return Results.BadRequest("Email is required for registration.");
            }

            // 1. Verify Attempt & Extract User Handle
            // We need the User Handle (ID) that was signed by the client.
            // This is critical because the authenticator is now bound to THAT ID.
            string? passedUserId = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(request.CredentialJson);
                if (doc.RootElement.TryGetProperty("response", out var responseElem) &&
                    responseElem.TryGetProperty("userHandle", out var userHandleElem))
                {
                    var userHandleBase64 = userHandleElem.GetString();
                    if (!string.IsNullOrEmpty(userHandleBase64))
                    {
                        passedUserId = DecodeBase64UrlToString(userHandleBase64);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Users.PasskeyExtractUserIdError(logger, ex);
            }

            var attestationNew = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
            if (!attestationNew.Succeeded)
            {
                return Results.BadRequest($"Attestation failed: {attestationNew.Failure?.Message}");
            }

            // Use the user ID from the request (sent by client from options response)
            // This is critical - the user ID must match what was in the passkey creation options
            var newUserId = request.UserId ?? passedUserId ?? Guid.CreateVersion7().ToString();

            if (string.IsNullOrEmpty(request.UserId))
            {
                Log.Users.PasskeyNoUserIdProvided(logger);
            }

            var newUser = new ApplicationUser
            {
                Id = Guid.Parse(newUserId!), // Force usage of the ID
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = !verificationRequired
            };

            // Create the user in DB
            var createResult = await userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                return Results.BadRequest(createResult.Errors);
            }

            if (userStore is IUserPasskeyStore<ApplicationUser> ps)
            {
                if (attestationNew.Passkey != null)
                {
                    await ps.AddOrUpdatePasskeyAsync(newUser, attestationNew.Passkey, cancellationToken);

                    // Persist the changes (the added passkey) to the database
                    var updateResult = await userManager.UpdateAsync(newUser);
                    if (!updateResult.Succeeded)
                    {
                        return Results.BadRequest(updateResult.Errors);
                    }
                }
                else
                {
                    Log.Users.PasskeyIsNull(logger);
                    return Results.BadRequest("Failed to create passkey");
                }
            }

            if (verificationRequired)
            {
                var code = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
                await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(newUser.Id, newUser.Email!, code, newUser.UserName!));

                // Don't auto-login when verification is required
                return Results.Ok(new { Message = "Registration successful. Please check your email to verify your account." });
            }

            // Auto Login - Issue Token (only when verification is not required)
            var roles = await userManager.GetRolesAsync(newUser);
            var accessToken = tokenService.GenerateAccessToken(newUser, tenantContext.TenantId, roles);
            var refreshToken = tokenService.GenerateRefreshToken();

            newUser.RefreshTokens.Add(new RefreshTokenInfo(
                refreshToken,
                DateTimeOffset.UtcNow.AddDays(7),
                DateTimeOffset.UtcNow,
                tenantContext.TenantId));
            _ = await userManager.UpdateAsync(newUser);

            return Results.Ok(new LoginResponse(
                 "Bearer",
                 accessToken,
                 3600,
                 refreshToken
             ));
        });

        // 3. Get Login Options
        _ = paramsGroup.MapPost("/assertion/options", async (
            [FromBody] PasskeyLoginOptionsRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            CancellationToken cancellationToken) =>
        {
            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(request.Email))
            {
                user = await userManager.FindByEmailAsync(request.Email);
            }

            // Security: Use consistent error message to prevent email enumeration
            const string genericError = "Invalid email or no passkeys registered.";

            // If user doesn't exist, return generic error
            if (user == null)
            {
                return Results.BadRequest(genericError);
            }

            // Check if user has any passkeys registered
            if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
            {
                var passkeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);
                if (passkeys.Count == 0)
                {
                    return Results.BadRequest(genericError);
                }
            }

            // Generate options (assertion challenge)
            var options = await signInManager.MakePasskeyRequestOptionsAsync(user);

            return Results.Content(options.ToString(), "application/json");
        });

        // 4. Login Passkey
        _ = paramsGroup.MapPost("/assertion/result", async (
            [FromBody] RegisterPasskeyRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ITenantContext tenantContext,
            ILogger<Program> logger,
            BookStore.ApiService.Services.JwtTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (string.IsNullOrEmpty(request.CredentialJson))
                {
                    return Results.BadRequest("Invalid credential data");
                }

                var result = await signInManager.PasskeySignInAsync(request.CredentialJson);

                if (result.Succeeded)
                {
                    // Find User to issue JWT
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(request.CredentialJson);
                        // Standard WebAuthn assertion response contains "response.userHandle"
                        string? userId = null;

                        if (doc.RootElement.TryGetProperty("response", out var responseElem) &&
                            responseElem.TryGetProperty("userHandle", out var userHandleElem))
                        {
                            var userHandleBase64 = userHandleElem.GetString();
                            if (!string.IsNullOrEmpty(userHandleBase64))
                            {
                                userId = DecodeBase64UrlToString(userHandleBase64);
                            }
                        }

                        if (!string.IsNullOrEmpty(userId))
                        {
                            // Best case: We have the User ID directly from the authenticator
                            var user = await userManager.FindByIdAsync(userId);
                            if (user != null)
                            {
                                // Issue tokens
                                return await IssueTokens(user, tokenService, userManager, tenantContext.TenantId);
                            }
                        }

                        // Fallback: Lookup by Credential ID (if ID not returned or user not found by ID)
                        // This is less reliable if 1 user has multiple credentials, but "id" is unique globally usually.
                        if (doc.RootElement.TryGetProperty("id", out var idElement))
                        {
                            var idString = idElement.GetString();
                            if (!string.IsNullOrEmpty(idString))
                            {
                                var credentialId = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Decode(idString);

                                if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                                {
                                    var user = await passkeyStore.FindByPasskeyIdAsync(credentialId, cancellationToken);
                                    if (user is not null)
                                    {
                                        return await IssueTokens(user, tokenService, userManager, tenantContext.TenantId);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Users.PasskeyParseError(logger, ex);
                    }

                    return Results.BadRequest("User not found for passkey.");
                }
                else
                {
                    Log.Users.PasskeyAssertionFailed(logger, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);
                    return Results.BadRequest("Invalid passkey assertion.");
                }
            }
            catch (Exception ex)
            {
                Log.Users.PasskeyLoginUnhandledException(logger, ex);
                return Results.BadRequest($"Login failed: {ex.Message}");
            }
        });

        // 5. List Passkeys (Authenticated)
        _ = paramsGroup.MapGet("/passkeys", async (
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
            {
                var passkeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);
                var response = passkeys.Select(p => new PasskeyInfo(
                    Id: Convert.ToBase64String(p.CredentialId),
                    Name: p.Name ?? "Passkey",
                    CreatedAt: p.CreatedAt
                )).ToList();
                return Results.Ok(response);
            }

            return Results.Ok(Array.Empty<PasskeyInfo>());
        }).RequireAuthorization();

        // 6. Delete Passkey (Authenticated)
        _ = paramsGroup.MapDelete("/passkeys/{id}", async (
            string id,
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var credentialId = Convert.FromBase64String(id);
                if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                {
                    // Prevent deleting last passkey if user has no password
                    var passkeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);
                    if (passkeys.Count <= 1)
                    {
                        var hasPassword = await userManager.HasPasswordAsync(user);
                        if (!hasPassword)
                        {
                            return Results.BadRequest("Cannot delete your only passkey. You would be locked out of your account. Set a password first.");
                        }
                    }

                    await passkeyStore.RemovePasskeyAsync(user, credentialId, cancellationToken);
                    _ = await userManager.UpdateAsync(user);
                    return Results.Ok(new { Message = "Passkey deleted." });
                }

                return Results.BadRequest("Passkey store not available.");
            }
            catch (FormatException)
            {
                return Results.BadRequest("Invalid passkey ID format.");
            }
        }).RequireAuthorization();

        return endpoints;
    }

    static async Task<IResult> IssueTokens(
        ApplicationUser user,
        BookStore.ApiService.Services.JwtTokenService tokenService,
        UserManager<ApplicationUser> userManager,
        string tenantId)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, tenantId, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshTokenInfo(
            refreshToken,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            tenantId));
        _ = await userManager.UpdateAsync(user);

        return Results.Ok(new LoginResponse(
            "Bearer",
            accessToken,
            3600,
            refreshToken
        ));
    }

    /// <summary>
    /// Decodes a Base64URL-encoded string to a UTF-8 string.
    /// </summary>
    static string DecodeBase64UrlToString(string base64Url)
    {
        var bytes = Base64UrlTextEncoder.Decode(base64Url);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}

public record RegisterPasskeyRequest(string CredentialJson, string? Email = null, string? UserId = null);
public record PasskeyCreationRequest(string? Email = null);
public record PasskeyLoginOptionsRequest(string? Email);
public record PasskeyInfo(string Id, string Name, DateTimeOffset? CreatedAt);
