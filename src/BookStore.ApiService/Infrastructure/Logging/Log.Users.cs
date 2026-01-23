using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

public static partial class Log
{
    public static partial class Users
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "JWT login attempt for {Email}")]
        public static partial void JwtLoginAttempt(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Login failed: User not found for {Email}")]
        public static partial void LoginFailedUserNotFound(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Login failed: Invalid password for {Email}")]
        public static partial void LoginFailedInvalidPassword(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "JWT login successful for {Email}")]
        public static partial void JwtLoginSuccessful(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "JWT registration attempt for {Email}")]
        public static partial void JwtRegistrationAttempt(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Registration failed for {Email}: {Errors}")]
        public static partial void RegistrationFailed(ILogger logger, string email, string errors);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "JWT registration successful for {Email}")]
        public static partial void JwtRegistrationSuccessful(ILogger logger, string email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Refresh failed: Token not found")]
        public static partial void RefreshFailedTokenNotFound(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Refresh failed: Token expired or invalid for user {User}")]
        public static partial void RefreshFailedTokenExpiredOrInvalid(ILogger logger, string? user);

        // Passkeys
        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Error extracting user ID from credential")]
        public static partial void PasskeyExtractUserIdError(ILogger logger, Exception ex);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "No user ID provided in request, using fallback - login may fail")]
        public static partial void PasskeyNoUserIdProvided(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Passkey is null after successful attestation")]
        public static partial void PasskeyIsNull(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Error parsing credential JSON for user lookup")]
        public static partial void PasskeyParseError(ILogger logger, Exception ex);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Unhandled exception during passkey login")]
        public static partial void PasskeyLoginUnhandledException(ILogger logger, Exception ex);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Logout successful for {User}")]
        public static partial void LogoutSuccessful(ILogger logger, string? user);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Passkey assertion failed. IsLockedOut: {IsLockedOut}, IsNotAllowed: {IsNotAllowed}, RequiresTwoFactor: {RequiresTwoFactor}")]
        public static partial void PasskeyAssertionFailed(ILogger logger, bool isLockedOut, bool isNotAllowed, bool requiresTwoFactor);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Adding passkey to existing user {Email}")]
        public static partial void PasskeyAttestationAttempt(ILogger logger, string? email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Passkey attestation failed for user {Email}: {Error}")]
        public static partial void PasskeyAttestationFailed(ILogger logger, string? email, string? error);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to update user {Email} after adding passkey: {Errors}")]
        public static partial void PasskeyUpdateUserFailed(ILogger logger, string? email, string errors);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Passkey added successfully for user {Email}")]
        public static partial void PasskeyRegistrationSuccessful(ILogger logger, string? email);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid GUID format for user ID from {Source}: {Value}. Generating new ID.")]
        public static partial void PasskeyInvalidGuidFormat(ILogger logger, string source, string value);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating new user for passkey registration with ID {UserId} from {Source}")]
        public static partial void PasskeyCreatingNewUser(ILogger logger, Guid userId, string source);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Registration failed: User ID {UserId} already exists.")]
        public static partial void PasskeyRegistrationIdConflict(ILogger logger, Guid userId);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Unhandled exception during passkey registration/addition")]
        public static partial void PasskeyRegistrationUnhandledException(ILogger logger, Exception ex);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Email confirmation failed: User not found {UserId}")]
        public static partial void ConfirmationFailedUserNotFound(ILogger logger, string userId);

        [LoggerMessage(
             Level = LogLevel.Warning,
             Message = "Email confirmation failed: Invalid code for user {UserId}. Errors: {Errors}")]
        public static partial void ConfirmationFailedInvalidCode(ILogger logger, string userId, string errors);

    }
}
