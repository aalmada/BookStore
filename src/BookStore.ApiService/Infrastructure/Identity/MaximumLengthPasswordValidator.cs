using BookStore.Shared.Validation;
using Microsoft.AspNetCore.Identity;

namespace BookStore.ApiService.Infrastructure.Identity;

/// <summary>
/// Enforces maximum password length to reduce password-hash DoS risk.
/// </summary>
public sealed class MaximumLengthPasswordValidator<TUser> : IPasswordValidator<TUser>
    where TUser : class
{
    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
    {
        if (password is null || password.Length <= PasswordValidator.MaxLength)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordTooLong",
            Description = $"At most {PasswordValidator.MaxLength} characters"
        }));
    }
}
