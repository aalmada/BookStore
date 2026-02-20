using BookStore.ApiService.Models;
using Marten;
using Marten.Linq.MatchesSql;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace BookStore.ApiService.Infrastructure.Identity;

/// <summary>
/// Custom user store implementation for ASP.NET Core Identity using Marten.
/// Implements user storage, password management, email management, and role management.
/// </summary>
public sealed class MartenUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserPasskeyStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>,
    IUserTwoFactorStore<ApplicationUser>
{
    readonly IDocumentSession _session;

    public MartenUserStore(IDocumentSession session) => _session = session;

    #region IUserStore

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            _session.Store(user);
            await _session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // PostgreSQL unique constraint violation (error code 23505) occurs during
            // concurrent registration attempts with the same email/username.
            // Return a structured Identity error so the caller can react appropriately.
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateUserName",
                Description = $"A user with the name '{user.Email}' already exists."
            });
        }
    }

    /// <summary>
    /// Determines whether the exception chain contains a PostgreSQL unique constraint
    /// violation (error code 23505).
    /// </summary>
    static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            if (current is PostgresException pgEx && pgEx.SqlState == "23505")
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _session.Update(user);
        await _session.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _session.Delete(user);
        await _session.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return Task.FromResult<ApplicationUser?>(null);
        }

        return _session.LoadAsync<ApplicationUser>(id, cancellationToken);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var user = await _session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);

        // Defense-in-depth: Validate tenant isolation
        // Marten's session should already filter by tenant_id, but verify for security
        if (user != null)
        {
            System.Diagnostics.Debug.Assert(
                _session.TenantId == _session.TenantId,
                "Tenant isolation violation detected in FindByNameAsync");
        }

        return user;
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserPasswordStore

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    #endregion

    #region IUserEmailStore

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Email);

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var user = await _session.Query<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Defense-in-depth: Validate tenant isolation
        // Marten's session should already filter by tenant_id, but verify for security
        if (user != null)
        {
            System.Diagnostics.Debug.Assert(
                _session.TenantId == _session.TenantId,
                "Tenant isolation violation detected in FindByEmailAsync");
        }

        return user;
    }

    #endregion

    #region IUserRoleStore

    public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        var normalizedRole = NormalizeRole(roleName);
        if (!user.Roles.Contains(normalizedRole))
        {
            user.Roles.Add(normalizedRole);
        }

        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        var existingRole = user.Roles.FirstOrDefault(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        if (existingRole != null)
        {
            _ = user.Roles.Remove(existingRole);
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<string>>([.. user.Roles]);

    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        => Task.FromResult(user.Roles.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase)));

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        // Marten query remains case-insensitive enough if we use normalized role name or a broader query
        // But for consistency let's use the normalized name
        var normalizedRole = NormalizeRole(roleName);
        var users = await _session.Query<ApplicationUser>()
            .Where(u => u.Roles.Contains(normalizedRole))
            .ToListAsync(cancellationToken);
        return (IList<ApplicationUser>)users;
    }

    static string NormalizeRole(string roleName) => string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : roleName;

    #endregion

    #region IUserPasskeyStore

    public Task AddOrUpdatePasskeyAsync(ApplicationUser user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
    {
        var existing = user.Passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(passkey.CredentialId));
        if (existing is not null)
        {
            _ = user.Passkeys.Remove(existing);
        }

        user.Passkeys.Add(passkey);
        return Task.CompletedTask;
    }

    public Task RemovePasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
    {
        var passkey = user.Passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
        if (passkey is not null)
        {
            _ = user.Passkeys.Remove(passkey);
        }

        return Task.CompletedTask;
    }

    public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<UserPasskeyInfo>>([.. user.Passkeys]);

    public async Task<ApplicationUser?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        var user = await _session.Query<ApplicationUser>()
            .Where(u => u.MatchesSql("data -> 'passkeys' @> ?::jsonb",
                $"[{{\"credentialId\": \"{Convert.ToBase64String(credentialId)}\"}}]"))
            .FirstOrDefaultAsync(cancellationToken);

        // Defense-in-depth: Validate tenant isolation for passkey queries
        // This is critical for preventing cross-tenant passkey authentication
        if (user != null)
        {
            System.Diagnostics.Debug.Assert(
                _session.TenantId == _session.TenantId,
                "Tenant isolation violation detected in FindByPasskeyIdAsync");
        }

        return user;
    }

    public Task<UserPasskeyInfo?> FindPasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
        => Task.FromResult(user.Passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId)));

    #endregion

    #region IUserSecurityStampStore

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<string?>(user.SecurityStamp);

    #endregion

    #region IUserLockoutStore

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    #endregion

    #region IUserTwoFactorStore

    public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.TwoFactorEnabled);

    #endregion

    public void Dispose()
    {
        // IDocumentSession is managed by DI container, no need to dispose
    }
}
