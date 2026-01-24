using BookStore.Client;
using BookStore.Shared.Models;
using BookStore.Shared.Validation;
using BookStore.Web.Infrastructure;

namespace BookStore.Web.Services;

/// <summary>
/// Service for managing user authentication using JWT token-based authentication
/// </summary>
public class AuthenticationService(
    IIdentityClient identityClient,
    TokenService tokenService,
    TenantService tenantService)
{
    public async Task<Result> ConfirmEmailAsync(string userId, string code)
    {
        try
        {
            await identityClient.ConfirmEmailAsync(userId, code);
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_CONFIRM_EMAIL_FAILED", ex.Message));
        }
    }

    public async Task<Result> ResendVerificationEmailAsync(string email)
    {
        try
        {
            await identityClient.ResendVerificationAsync(new ResendVerificationRequest(email));
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_RESEND_VERIFICATION_FAILED", ex.Message));
        }
    }

    /// <summary>
    /// Login with email and password (JWT token-based)
    /// </summary>
    public async Task<Result<LoginResponse>> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest(email, password);
            // useCookies=false - we want JWT tokens, not cookies
            var response = await identityClient.LoginAsync(request, useCookies: false);

            return Result.Success(response);
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult<LoginResponse>();
        }
        catch (Exception ex)
        {
            return Result.Failure<LoginResponse>(Error.Failure("ERR_LOGIN_FAILED", ex.Message));
        }
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    public async Task<Result> RegisterAsync(string email, string password)
    {
        // Validate password strength
        var validationError = ValidatePassword(password);
        if (validationError != null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Auth.PasswordMismatch, validationError));
        }

        try
        {
            var request = new RegisterRequest(email, password);
            _ = await identityClient.RegisterAsync(request);
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_REGISTRATION_FAILED", ex.Message));
        }
    }

    /// <summary>
    /// Logout the current user and invalidate refresh token on server
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var currentTenant = tenantService.CurrentTenantId;
            var refreshToken = tokenService.GetRefreshToken(currentTenant);
            await identityClient.LogoutAsync(new LogoutRequest(refreshToken));
        }
        catch
        {
            // Logout failures are non-critical - local tokens will still be cleared
        }
    }

    /// <summary>
    /// Validate password strength using shared validator
    /// </summary>
    static string? ValidatePassword(string password) => PasswordValidator.GetFirstError(password);

    public async Task<Result> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError != null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Auth.PasswordMismatch, validationError));
        }

        if (currentPassword == newPassword)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Auth.PasswordReuse,
                "New password cannot be the same as the current password."));
        }

        try
        {
            await identityClient.ChangePasswordAsync(new ChangePasswordRequest(currentPassword, newPassword));
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_CHANGE_PASSWORD_FAILED", ex.Message));
        }
    }

    public async Task<Result> AddPasswordAsync(string newPassword)
    {
        var validationError = ValidatePassword(newPassword);
        if (validationError != null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Auth.PasswordMismatch, validationError));
        }

        try
        {
            await identityClient.AddPasswordAsync(new AddPasswordRequest(newPassword));
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_ADD_PASSWORD_FAILED", ex.Message));
        }
    }

    public async Task<Result> RemovePasswordAsync()
    {
        try
        {
            await identityClient.RemovePasswordAsync(new RemovePasswordRequest());
            return Result.Success();
        }
        catch (Refit.ApiException ex)
        {
            return ex.ToResult();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("ERR_REMOVE_PASSWORD_FAILED", ex.Message));
        }
    }

    public async Task<bool> HasPasswordAsync()
    {
        try
        {
            var response = await identityClient.GetPasswordStatusAsync();
            return response.HasPassword;
        }
        catch
        {
            return false;
        }
    }
}

