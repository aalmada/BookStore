using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Messages.Events;
using Marten;
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
        _ = paramsGroup.MapPost("/attestation/options", async Task<IResult> (
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
                return Result.Failure(Error.Validation(ErrorCodes.Passkey.EmailRequired, "Email is required for new registration.")).ToProblemDetails();
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
        _ = paramsGroup.MapPost("/attestation/result", async Task<IResult> (
            HttpContext context,
            [FromBody] RegisterPasskeyRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore,
            [FromServices] IDocumentSession session,
            ITenantContext tenantContext,
            BookStore.ApiService.Services.JwtTokenService tokenService,
            Wolverine.IMessageBus bus,
            ILogger<Program> logger,
            Microsoft.Extensions.Options.IOptions<Infrastructure.Email.EmailOptions> emailOptions,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var user = await userManager.GetUserAsync(context.User);
                var verificationRequired = emailOptions.Value.DeliveryMethod != "None";

                // Case A: Add to existing account
                if (user is not null)
                {
                    Log.Users.PasskeyAttestationAttempt(logger, user.Email);
                    var attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
                    if (!attestation.Succeeded)
                    {
                        Log.Users.PasskeyAttestationFailed(logger, user.Email, attestation.Failure?.Message);
                        return Result.Failure(Error.Validation(ErrorCodes.Passkey.AttestationFailed, $"Attestation failed: {attestation.Failure?.Message}")).ToProblemDetails();
                    }

                    // Capture Device Name from User-Agent
                    var clientUserAgent = context.Request.Headers.UserAgent.ToString();
                    if (attestation.Passkey != null)
                    {
                        attestation.Passkey.Name = BookStore.ApiService.Infrastructure.DeviceNameParser.Parse(clientUserAgent);
                    }

                    if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                    {
                        await passkeyStore.AddOrUpdatePasskeyAsync(user, attestation.Passkey!, cancellationToken);
                        // Persist the changes to the database
                        var updateResult = await userManager.UpdateAsync(user);
                        if (!updateResult.Succeeded)
                        {
                            Log.Users.PasskeyUpdateUserFailed(logger, user.Email, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                            return Result.Failure(Error.Validation(ErrorCodes.Passkey.InvalidCredential, string.Join(", ", updateResult.Errors.Select(e => e.Description)))).ToProblemDetails();
                        }
                    }

                    Log.Users.PasskeyRegistrationSuccessful(logger, user.Email);
                    return Results.Ok(new { Message = "Passkey added." });
                }

                // Case B: Sign Up new account
                // We need the email to create the user.
                if (string.IsNullOrEmpty(request.Email))
                {
                    return Result.Failure(Error.Validation(ErrorCodes.Passkey.EmailRequired, "Email is required for registration.")).ToProblemDetails();
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
                    return Result.Failure(Error.Validation(ErrorCodes.Passkey.AttestationFailed, $"Attestation failed: {attestationNew.Failure?.Message}")).ToProblemDetails();
                }

                // Capture Device Name from User-Agent
                var registrationUserAgent = context.Request.Headers.UserAgent.ToString();
                if (attestationNew.Passkey != null)
                {
                    attestationNew.Passkey.Name = BookStore.ApiService.Infrastructure.DeviceNameParser.Parse(registrationUserAgent);
                }

                // Use the user ID from the request (sent by client from options response)
                // This is critical - the user ID must match what was in the passkey creation options
                var userIdSource = "none";
                string? newUserIdString = null;

                if (!string.IsNullOrEmpty(passedUserId))
                {
                    newUserIdString = passedUserId;
                    userIdSource = "passedUserId (from userHandle)";
                }
                else if (!string.IsNullOrEmpty(request.UserId))
                {
                    newUserIdString = request.UserId;
                    userIdSource = "request.UserId";
                }
                else
                {
                    newUserIdString = Guid.CreateVersion7().ToString();
                    userIdSource = "newly generated";
                    Log.Users.PasskeyNoUserIdProvided(logger);
                }

                if (!Guid.TryParse(newUserIdString, out var newUserGuid))
                {
                    Log.Users.PasskeyInvalidGuidFormat(logger, userIdSource, newUserIdString!);
                    newUserGuid = Guid.CreateVersion7();
                }

                // SECURITY FIX: Check if user already exists to prevent overwrite
                var conflictingUserById = await userManager.FindByIdAsync(newUserGuid.ToString());
                if (conflictingUserById != null)
                {
                    Log.Users.PasskeyRegistrationIdConflict(logger, newUserGuid);
                    return Result.Failure(Error.Conflict(ErrorCodes.Passkey.IdAlreadyExists, "User ID already exists.")).ToProblemDetails();
                }

                // SECURITY FIX: Check if email already exists to prevent duplicate users
                var conflictingUserByEmail = await userManager.FindByEmailAsync(request.Email);
                if (conflictingUserByEmail != null)
                {
                    // Mask the error to prevent email enumeration
                    Log.Users.RegistrationFailed(logger, request.Email, "Passkey Registration: User already exists (masked)");
                    return Results.Ok(new { Message = "Registration successful. Please check your email to verify your account." });
                }

                Log.Users.PasskeyCreatingNewUser(logger, newUserGuid, userIdSource);

                var newUser = new ApplicationUser
                {
                    Id = newUserGuid,
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = !verificationRequired,
                    LockoutEnabled = true
                };

                // Create the user in DB
                var createResult = await userManager.CreateAsync(newUser);
                if (!createResult.Succeeded)
                {
                    // Security: Mask "User already exists" errors
                    if (createResult.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
                    {
                        Log.Users.RegistrationFailed(logger, request.Email, "Passkey Registration: User already exists (masked)");

                        // Return success message.
                        // Note: If email verification is required, we might trigger a "Password Reset" or "Account Exists" email here in a real app.
                        // For now, we mimic the success response.
                        return Results.Ok(new { Message = "Registration successful. Please check your email to verify your account." });
                    }

                    return Result.Failure(Error.Validation(ErrorCodes.Auth.InvalidCredentials, string.Join(", ", createResult.Errors.Select(e => e.Description)))).ToProblemDetails();
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
                            return Result.Failure(Error.Validation(ErrorCodes.Passkey.InvalidCredential, string.Join(", ", updateResult.Errors.Select(e => e.Description)))).ToProblemDetails();
                        }
                    }
                    else
                    {
                        Log.Users.PasskeyIsNull(logger);
                        return Result.Failure(Error.Validation(ErrorCodes.Passkey.AttestationFailed, "Failed to create passkey")).ToProblemDetails();
                    }
                }

                if (verificationRequired)
                {
                    var code = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
                    await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(
                        newUser.Id,
                        newUser.Email!,
                        code,
                        newUser.UserName!,
                        tenantContext.TenantId));

                    // Don't auto-login when verification is required
                    return Results.Ok(new { Message = "Registration successful. Please check your email to verify your account." });
                }

                // Initialize UserProfile stream for immediate access
                _ = session.Events.StartStream<BookStore.ApiService.Projections.UserProfile>(
                    newUser.Id,
                    new UserProfileCreated(newUser.Id));
                await session.SaveChangesAsync(cancellationToken);

                // Auto Login - Issue Token (only when verification is not required)
                // Build claims and generate tokens
                var accessToken = tokenService.GenerateAccessToken(newUser, tenantContext.TenantId, []);
                var refreshToken = tokenService.RotateRefreshToken(newUser, tenantContext.TenantId);

                _ = await userManager.UpdateAsync(newUser);

                return Results.Ok(new LoginResponse(
                    "Bearer",
                    accessToken,
                    3600,
                    refreshToken
                ));
            }
            catch (Exception ex)
            {
                Log.Users.PasskeyRegistrationUnhandledException(logger, ex);
                return Result.Failure(Error.InternalServerError("ERR_INTERNAL_SERVER_ERROR", $"Registration failed: {ex.Message}")).ToProblemDetails();
            }
        });

        // 3. Get Login Options
        _ = paramsGroup.MapPost("/assertion/options", async Task<IResult> (
            [FromBody] PasskeyLoginOptionsRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILogger<Program> logger,
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
                Log.Users.PasskeyOptionsUserNotFound(logger, request.Email ?? "(no email)");
                return Result.Failure(Error.Validation(ErrorCodes.Passkey.UserNotFound, genericError)).ToProblemDetails();
            }

            // Check if user has any passkeys registered
            if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
            {
                var passkeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);
                if (passkeys.Count == 0)
                {
                    Log.Users.PasskeyOptionsNoPasskeys(logger, request.Email ?? "(no email)");
                    return Result.Failure(Error.Validation(ErrorCodes.Passkey.UserNotFound, genericError)).ToProblemDetails();
                }

                // SECURITY: Log passkey details for debugging (credential IDs are not sensitive)
                Log.Users.PasskeyOptionsGenerated(logger, user.Id, user.Email ?? "(no email)", passkeys.Count);
            }

            // Generate options (assertion challenge)
            var options = await signInManager.MakePasskeyRequestOptionsAsync(user);

            return Results.Content(options.ToString(), "application/json");
        });

        // 4. Login Passkey
        _ = paramsGroup.MapPost("/assertion/result", async Task<IResult> (
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
                    return Result.Failure(Error.Validation(ErrorCodes.Passkey.InvalidCredential, "Invalid credential data")).ToProblemDetails();
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
                                // SECURITY: Verify the passkey actually belongs to this user
                                if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                                {
                                    var userPasskeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);

                                    // Extract credential ID from the assertion
                                    byte[]? credentialId = null;
                                    if (doc.RootElement.TryGetProperty("id", out var credIdElement))
                                    {
                                        var idString = credIdElement.GetString();
                                        if (!string.IsNullOrEmpty(idString))
                                        {
                                            credentialId = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Decode(idString);
                                        }
                                    }

                                    if (credentialId != null)
                                    {
                                        var matchingPasskey = userPasskeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
                                        if (matchingPasskey == null)
                                        {
                                            Log.Users.PasskeyCredentialMismatch(logger, Convert.ToBase64String(credentialId), userId);
                                            return Result.Failure(Error.Unauthorized(ErrorCodes.Passkey.AssertionFailed, "Invalid passkey assertion.")).ToProblemDetails();
                                        }

                                        Log.Users.PasskeyLoginSuccessfulWithCredential(logger, userId, Convert.ToBase64String(credentialId));
                                    }
                                }

                                // Issue tokens
                                // Check if user is allowed to sign in
                                if (!await signInManager.CanSignInAsync(user))
                                {
                                    if (userManager.Options.SignIn.RequireConfirmedEmail && !await userManager.IsEmailConfirmedAsync(user))
                                    {
                                        Log.Users.LoginFailedUnconfirmedEmail(logger, user.Email!);
                                        return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.EmailUnconfirmed, "Please confirm your email address.")).ToProblemDetails();
                                    }

                                    Log.Users.LoginFailedUserNotFound(logger, user.Email!);
                                    return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.NotAllowed, "User is not allowed to sign in.")).ToProblemDetails();
                                }

                                var roles = await userManager.GetRolesAsync(user);
                                var accessToken = tokenService.GenerateAccessToken(user, tenantContext.TenantId, roles);
                                var refreshToken = tokenService.RotateRefreshToken(user, tenantContext.TenantId);

                                _ = await userManager.UpdateAsync(user);

                                return Results.Ok(new LoginResponse(
                                    "Bearer",
                                    accessToken,
                                    3600,
                                    refreshToken
                                ));
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
                                        // Issue tokens
                                        if (!await signInManager.CanSignInAsync(user))
                                        {
                                            if (userManager.Options.SignIn.RequireConfirmedEmail && !await userManager.IsEmailConfirmedAsync(user))
                                            {
                                                Log.Users.LoginFailedUnconfirmedEmail(logger, user.Email!);
                                                return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.EmailUnconfirmed, "Please confirm your email address.")).ToProblemDetails();
                                            }

                                            Log.Users.LoginFailedUserNotFound(logger, user.Email!);
                                            return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.NotAllowed, "User is not allowed to sign in.")).ToProblemDetails();
                                        }

                                        var roles = await userManager.GetRolesAsync(user);
                                        var accessToken = tokenService.GenerateAccessToken(user, tenantContext.TenantId, roles);
                                        var refreshToken = tokenService.RotateRefreshToken(user, tenantContext.TenantId);

                                        _ = await userManager.UpdateAsync(user);

                                        return Results.Ok(new LoginResponse(
                                            "Bearer",
                                            accessToken,
                                            3600,
                                            refreshToken
                                        ));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Users.PasskeyParseError(logger, ex);
                    }

                    return Result.Failure(Error.NotFound(ErrorCodes.Passkey.UserNotFound, "User not found for passkey.")).ToProblemDetails();
                }
                else
                {
                    Log.Users.PasskeyAssertionFailed(logger, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);
                    return Result.Failure(Error.Unauthorized(ErrorCodes.Passkey.AssertionFailed, "Invalid passkey assertion.")).ToProblemDetails();
                }
            }
            catch (Exception ex)
            {
                Log.Users.PasskeyLoginUnhandledException(logger, ex);
                return Result.Failure(Error.InternalServerError("ERR_INTERNAL_SERVER_ERROR", $"Login failed: {ex.Message}")).ToProblemDetails();
            }
        });

        // 5. List Passkeys (Authenticated)
        _ = paramsGroup.MapGet("/passkeys", async Task<IResult> (
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
                    Id: Base64UrlTextEncoder.Encode(p.CredentialId),
                    Name: p.Name ?? "Passkey",
                    CreatedAt: p.CreatedAt
                )).ToList();
                return Results.Ok(response);
            }

            return Results.Ok(Array.Empty<PasskeyInfo>());
        }).RequireAuthorization();

        // 6. Delete Passkey (Authenticated)
        _ = paramsGroup.MapDelete("/passkeys/{id}", async Task<IResult> (
            string id,
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Result.Failure(Error.Unauthorized(ErrorCodes.Auth.InvalidToken, "User not found.")).ToProblemDetails();
            }

            try
            {
                var credentialId = Base64UrlTextEncoder.Decode(id);
                if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                {
                    // Prevent deleting last passkey if user has no password
                    var passkeys = await passkeyStore.GetPasskeysAsync(user, cancellationToken);
                    if (passkeys.Count <= 1)
                    {
                        var hasPassword = await userManager.HasPasswordAsync(user);
                        if (!hasPassword)
                        {
                            return Result.Failure(Error.Validation(ErrorCodes.Passkey.LastPasskey, "Cannot delete your only passkey. You would be locked out of your account. Set a password first.")).ToProblemDetails();
                        }
                    }

                    await passkeyStore.RemovePasskeyAsync(user, credentialId, cancellationToken);
                    _ = await userManager.UpdateAsync(user);
                    return Results.Ok(new { Message = "Passkey deleted." });
                }

                return Result.Failure(Error.Validation(ErrorCodes.Passkey.StoreNotAvailable, "Passkey store not available.")).ToProblemDetails();
            }
            catch (FormatException)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Passkey.InvalidFormat, "Invalid passkey ID format.")).ToProblemDetails();
            }
        }).RequireAuthorization();

        return endpoints;
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
