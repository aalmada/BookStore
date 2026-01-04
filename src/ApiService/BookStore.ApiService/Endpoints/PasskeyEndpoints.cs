using System.Security.Claims;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints;

public static class PasskeyEndpoints
{
    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var paramsGroup = endpoints.MapGroup("/Account");

        // 1. Get Creation Options (Authenticated & Unauthenticated)
        _ = paramsGroup.MapPost("/PasskeyCreationOptions", async (
            HttpContext context,
            [FromBody] PasskeyCreationRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var user = await userManager.GetUserAsync(context.User);

            // If authenticated, use current user (Add Passkey logic)
            if (user is not null)
            {
                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";
                var userEntity = new PasskeyUserEntity { Id = userId, Name = userName, DisplayName = userName };
                return Results.Json(await signInManager.MakePasskeyCreationOptionsAsync(userEntity));
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

            return Results.Json(await signInManager.MakePasskeyCreationOptionsAsync(newUserEntity));
        });

        // 2. Register Passkey (Finish Registration)
        _ = paramsGroup.MapPost("/RegisterPasskey", async (
            HttpContext context,
            [FromBody] RegisterPasskeyRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore,
            BookStore.ApiService.Services.JwtTokenService tokenService,
            Wolverine.IMessageBus bus,
            Microsoft.Extensions.Options.IOptions<Infrastructure.Email.EmailOptions> emailOptions) =>
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
                    await passkeyStore.AddOrUpdatePasskeyAsync(user, attestation.Passkey, CancellationToken.None);
                }

                return Results.Ok(new { Message = "Passkey added." });
            }

            // Case B: Sign Up new account
            // We need the email to create the user.
            if (string.IsNullOrEmpty(request.Email))
            {
                return Results.BadRequest("Email is required for registration.");
            }

            var attestationNew = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
            if (!attestationNew.Succeeded)
            {
                return Results.BadRequest($"Attestation failed: {attestationNew.Failure?.Message}");
            }

            var newUser = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = !verificationRequired
            };

            // Create the user in DB
            // Note: passkey attestation is valid, but we haven't saved the user yet.
            var createResult = await userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                return Results.BadRequest(createResult.Errors);
            }

            if (userStore is IUserPasskeyStore<ApplicationUser> ps)
            {
                await ps.AddOrUpdatePasskeyAsync(newUser, attestationNew.Passkey, CancellationToken.None);

                // Persist the changes (the added passkey) to the database
                var updateResult = await userManager.UpdateAsync(newUser);
                if (!updateResult.Succeeded)
                {
                    return Results.BadRequest(updateResult.Errors);
                }
            }

            if (verificationRequired)
            {
                var code = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
                await bus.PublishAsync(new Messages.Commands.SendUserVerificationEmail(newUser.Id, newUser.Email!, code, newUser.UserName!));
            }

            // Auto Login - Issue Token
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
        _ = paramsGroup.MapPost("/PasskeyLoginOptions", async (
            [FromBody] PasskeyLoginOptionsRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(request.Email))
            {
                user = await userManager.FindByEmailAsync(request.Email);
            }

            // Generate options (assertion challenge)
            // If user is null, this should generate options for "discoverable" credentials (empty allowCredentials)
            var options = await signInManager.MakePasskeyRequestOptionsAsync(user);

            return Results.Json(options);
        });

        // 4. Login Passkey
        _ = paramsGroup.MapPost("/LoginPasskey", async (
            [FromBody] RegisterPasskeyRequest request,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            BookStore.ApiService.Services.JwtTokenService tokenService) =>
        {
            // 1. Verify Assertion
            var result = await signInManager.PasskeySignInAsync(request.CredentialJson); // Corrected to 1 arg

            if (result.Succeeded)
            {
                // 2. Find User to issue JWT
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(request.CredentialJson);
                    if (doc.RootElement.TryGetProperty("id", out var idElement))
                    {
                        var idString = idElement.GetString();
                        if (!string.IsNullOrEmpty(idString))
                        {
                            var credentialId = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Decode(idString);

                            if (userStore is IUserPasskeyStore<ApplicationUser> passkeyStore)
                            {
                                var user = await passkeyStore.FindByPasskeyIdAsync(credentialId, CancellationToken.None);
                                if (user is not null)
                                {
                                    // 3. Generate Tokens
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
                        }
                    }
                }
                catch { /* Parsing failed */ }

                return Results.BadRequest("User not found for passkey.");
            }
            else
            {
                return Results.BadRequest("Invalid passkey assertion.");
            }
        });

        return endpoints;
    }
}

public record RegisterPasskeyRequest(string CredentialJson, string? Email = null);
public record PasskeyCreationRequest(string? Email = null);
public record PasskeyLoginOptionsRequest(string? Email);
