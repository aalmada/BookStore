using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class PasskeyEndpoints
{
    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var paramsGroup = endpoints.MapGroup("/account").RequireRateLimiting("AuthPolicy");

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

                // The MakePasskeyCreationOptionsAsync method returns a JSON string in this version of Identity.
                // We must return it as raw content to avoid double-serialization.
                var options = await signInManager.MakePasskeyCreationOptionsAsync(userEntity);
                return Results.Content(options.ToString(), "application/json");
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
            var response = new
            {
                options = System.Text.Json.JsonDocument.Parse(newOptions.ToString()).RootElement,
                userId = newUserId
            };

            return Results.Ok(response);
        });

        // 2. Register Passkey (Finish Registration)
        _ = paramsGroup.MapPost("/attestation/result", async (
            HttpContext context,
            [FromBody] RegisterPasskeyRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore,
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
                        // URL-safe Base64 decode
                        var cleanBase64 = userHandleBase64.Replace('-', '+').Replace('_', '/');
                        switch (cleanBase64.Length % 4)
                        {
                            case 2: cleanBase64 += "=="; break;
                            case 3: cleanBase64 += "="; break;
                        }

                        var bytes = Convert.FromBase64String(cleanBase64);
                        passedUserId = System.Text.Encoding.UTF8.GetString(bytes);
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
            var accessToken = tokenService.GenerateAccessToken(newUser);
            var refreshToken = tokenService.GenerateRefreshToken();

            newUser.RefreshTokens.Add(new RefreshTokenInfo(refreshToken, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow));
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
            CancellationToken cancellationToken) =>
        {
            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(request.Email))
            {
                user = await userManager.FindByEmailAsync(request.Email);
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
                                var cleanBase64 = userHandleBase64.Replace('-', '+').Replace('_', '/');
                                switch (cleanBase64.Length % 4)
                                {
                                    case 2: cleanBase64 += "=="; break;
                                    case 3: cleanBase64 += "="; break;
                                }

                                var bytes = Convert.FromBase64String(cleanBase64);
                                userId = System.Text.Encoding.UTF8.GetString(bytes);
                            }
                        }

                        if (!string.IsNullOrEmpty(userId))
                        {
                            // Best case: We have the User ID directly from the authenticator
                            var user = await userManager.FindByIdAsync(userId);
                            if (user != null)
                            {
                                // Issue tokens
                                return await IssueTokens(user, tokenService, userManager);
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
                                        return await IssueTokens(user, tokenService, userManager);
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
                    return Results.BadRequest("Invalid passkey assertion.");
                }
            }
            catch (Exception ex)
            {
                Log.Users.PasskeyLoginUnhandledException(logger, ex);
                return Results.BadRequest($"Login failed: {ex.Message}");
            }
        });

        return endpoints;
    }

    static async Task<IResult> IssueTokens(
        ApplicationUser user,
        BookStore.ApiService.Services.JwtTokenService tokenService,
        UserManager<ApplicationUser> userManager)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshTokenInfo(refreshToken, DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow));
        _ = await userManager.UpdateAsync(user);

        return Results.Ok(new LoginResponse(
            "Bearer",
            accessToken,
            3600,
            refreshToken
        ));
    }
}

public record RegisterPasskeyRequest(string CredentialJson, string? Email = null, string? UserId = null);
public record PasskeyCreationRequest(string? Email = null);
public record PasskeyLoginOptionsRequest(string? Email);
