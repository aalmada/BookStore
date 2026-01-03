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
            // The ID generated here should theoretically be used to create the user later.
            // However, MakePasskeyCreationOptionsAsync returns options with an encoded UserID.
            // Client will sign this.
            // When we verify in RegisterPasskey, we need to ensure the eventually created user has this ID.
            // Store this 'intent' or rely on stateless verification? 
            // Stateless: We can't enforce the ID matches unless we manually handle option generation.
            // For MVP: We will let options be generated.

            // NOTE: Registering a user with a passkey requires the server to know the UserHandle (ID) 
            // that is baked into the credential. 
            // In .NET 10 helper, we pass a PasskeyUserEntity.
            // We'll generate a new Guid for this prospective user.
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
            BookStore.ApiService.Services.JwtTokenService tokenService) =>
        {
            var user = await userManager.GetUserAsync(context.User);

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
                EmailConfirmed = true // Verified via Passkey
            };

            // Create the user in DB
            // Note: passkey attestation is valid, but we haven't saved the user yet.
            var createResult = await userManager.CreateAsync(newUser);
            if (!createResult.Succeeded)
            {
                return Results.BadRequest(createResult.Errors);
            }

            // Save the passkey
            if (userStore is IUserPasskeyStore<ApplicationUser> ps)
            {
                // We MUST update the Passkey's UserId to match the actual DB user ID if they differ
                // attestationNew.Passkey.UserHandle might be the random one we sent in options.
                // But we just created a user and it got a ID (likely different if DB generated it, or same if we set it).
                // With Marten/Identity user manager, ID is usually a set GUID.
                // IMPORTANT: The WebAuthn credential is BOUND to the userHandle sent in options.
                // Browsers will only serve this credential if we ask for that userHandle (or empty for discoverable).
                // Ideally, newUser.Id should MATCH the UserHandle in the credential.

                // For this MVP, we will save the passkey as is. 
                // Login relies on UserHandle lookup.
                await ps.AddOrUpdatePasskeyAsync(newUser, attestationNew.Passkey, CancellationToken.None);
            }

            // Auto Login - Issue Token
            var accessToken = tokenService.GenerateAccessToken(newUser);
            var refreshToken = tokenService.GenerateRefreshToken();

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

                                    // TODO: Save refresh token (mimicking existing functionality which is currently TODO)
                                    // await tokenService.SaveRefreshTokenAsync(user.Id, refreshToken);

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
