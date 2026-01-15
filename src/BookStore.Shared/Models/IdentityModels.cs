namespace BookStore.Shared.Models;

/// <summary>
/// Request to login with email and password
/// </summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Request to register a new user
/// </summary>
public record RegisterRequest(string Email, string Password);

/// <summary>
/// Request to refresh an access token
/// </summary>
public record RefreshRequest(string RefreshToken);

/// <summary>
/// Response from login or refresh endpoints
/// </summary>
public record LoginResponse(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);

/// <summary>
/// User information
/// </summary>
public record UserInfo(
    string Email,
    bool IsEmailConfirmed);

/// <summary>
/// Request to update user information
/// </summary>
public record UpdateUserInfoRequest(
    string? NewEmail = null,
    string? NewPassword = null,
    string? OldPassword = null);
