using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BookStore.ApiService.Endpoints;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Endpoints;

public class PasskeyAccountRecoveryProblemDetailsTests
{
    [Test]
    [Category("Unit")]
    public async Task RemovePasswordAsync_WhenUserHasNoPasskeys_ShouldReturnActionableProblemDetails()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "user@example.com" };
        var store = new StubPasskeyStore([]);
        var userManager = new StubUserManager(store)
        {
            CurrentUser = user,
            HasPassword = true
        };

        // Act
        var result = await InvokeRemovePasswordAsync(userManager, store, user);
        var response = await ExecuteResultAsync(result);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(response.ContentType).IsEqualTo("application/problem+json");

        var root = response.Document.RootElement;
        _ = await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("Cannot Remove Password Without a Passkey");

        var detail = root.GetProperty("detail").GetString();
        _ = await Assert.That(detail).IsNotNull();
        _ = await Assert.That(detail!).Contains("register at least one passkey");
        _ = await Assert.That(detail).Contains("contact an administrator");
        _ = await Assert.That(root.GetProperty("error").GetString()).IsEqualTo(ErrorCodes.Auth.InvalidRequest);
    }

    [Test]
    [Category("Unit")]
    public async Task DeletePasskeyAsync_WhenDeletingLastPasskeyWithoutPassword_ShouldReturnActionableProblemDetails()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), Email = "user@example.com" };
        var passkey = CreatePasskey("primary-passkey");
        user.Passkeys.Add(passkey);

        var store = new StubPasskeyStore([passkey]);
        var userManager = new StubUserManager(store)
        {
            CurrentUser = user,
            HasPassword = false
        };

        var passkeyId = Base64UrlTextEncoder.Encode(passkey.CredentialId);

        // Act
        var result = await InvokeDeletePasskeyAsync(passkeyId, userManager, store, user);
        var response = await ExecuteResultAsync(result);

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(response.ContentType).IsEqualTo("application/problem+json");

        var root = response.Document.RootElement;
        _ = await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status400BadRequest);
        _ = await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("Cannot Remove Your Last Passkey");

        var detail = root.GetProperty("detail").GetString();
        _ = await Assert.That(detail).IsNotNull();
        _ = await Assert.That(detail!).Contains("Add a password while signed in");
        _ = await Assert.That(detail).Contains("register a replacement passkey");
        _ = await Assert.That(detail).Contains("contact an administrator");
        _ = await Assert.That(root.GetProperty("error").GetString()).IsEqualTo(ErrorCodes.Passkey.LastPasskey);
    }

    static UserPasskeyInfo CreatePasskey(string seed)
        => new(
            credentialId: Encoding.UTF8.GetBytes(seed),
            publicKey: [],
            createdAt: DateTimeOffset.UtcNow,
            signCount: 0,
            transports: [],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: true,
            attestationObject: [],
            clientDataJson: []);

    static async Task<IResult> InvokeRemovePasswordAsync(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        ApplicationUser user)
    {
        var method = typeof(JwtAuthenticationEndpoints).GetMethod("RemovePasswordAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("RemovePasswordAsync method was not found.");

        var invocation = method.Invoke(null,
        [
            new RemovePasswordRequest(),
            userManager,
            null!, // HybridCache is only used on successful removal.
            null!, // ITenantContext is only used on successful removal.
            userStore,
            CreatePrincipal(user),
            CancellationToken.None
        ]);

        var task = (Task<IResult>?)invocation
            ?? throw new InvalidOperationException("RemovePasswordAsync did not return Task<IResult>.");

        return await task;
    }

    static async Task<IResult> InvokeDeletePasskeyAsync(
        string passkeyId,
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        ApplicationUser user)
    {
        var method = typeof(PasskeyEndpoints).GetMethod("DeletePasskeyAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeletePasskeyAsync method was not found.");

        var invocation = method.Invoke(null,
        [
            passkeyId,
            CreatePrincipal(user),
            userManager,
            null!, // HybridCache is only used on successful removal.
            null!, // ITenantContext is only used on successful removal.
            userStore,
            CancellationToken.None
        ]);

        var task = (Task<IResult>?)invocation
            ?? throw new InvalidOperationException("DeletePasskeyAsync did not return Task<IResult>.");

        return await task;
    }

    static ClaimsPrincipal CreatePrincipal(ApplicationUser user)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "Test"));

    static async Task<(int StatusCode, string? ContentType, JsonDocument Document)> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddProblemDetails();

        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext();
        context.RequestServices = serviceProvider;
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        var document = await JsonDocument.ParseAsync(body);
        return (context.Response.StatusCode, context.Response.ContentType, document);
    }

    sealed class StubUserManager(IUserStore<ApplicationUser> store)
        : UserManager<ApplicationUser>(
            store,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public ApplicationUser? CurrentUser { get; init; }

        public bool HasPassword { get; init; }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
            => Task.FromResult(CurrentUser);

        public override Task<bool> HasPasswordAsync(ApplicationUser user)
            => Task.FromResult(HasPassword);
    }

    sealed class StubPasskeyStore(IReadOnlyList<UserPasskeyInfo> passkeys)
        : IUserStore<ApplicationUser>, IUserPasskeyStore<ApplicationUser>
    {
        readonly List<UserPasskeyInfo> _passkeys = [.. passkeys];

        public void Dispose()
        {
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult<string?>(user.NormalizedUserName);

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task AddOrUpdatePasskeyAsync(ApplicationUser user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
        {
            var existing = _passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(passkey.CredentialId));
            if (existing is not null)
            {
                _ = _passkeys.Remove(existing);
            }

            _passkeys.Add(passkey);
            return Task.CompletedTask;
        }

        public Task<ApplicationUser?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
            => Task.FromResult<ApplicationUser?>(null);

        public Task<UserPasskeyInfo?> FindPasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
            => Task.FromResult(_passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId)));

        public Task<IList<UserPasskeyInfo>> GetPasskeysAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult<IList<UserPasskeyInfo>>([.. _passkeys]);

        public Task RemovePasskeyAsync(ApplicationUser user, byte[] credentialId, CancellationToken cancellationToken)
        {
            var existing = _passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
            if (existing is not null)
            {
                _ = _passkeys.Remove(existing);
            }

            return Task.CompletedTask;
        }
    }
}
