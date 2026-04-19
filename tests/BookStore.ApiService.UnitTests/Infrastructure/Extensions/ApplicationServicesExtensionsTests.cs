using System.Security.Claims;
using System.Security.Cryptography;
using BookStore.ApiService.Infrastructure.Email;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Identity;
using BookStore.ApiService.Models;
using BookStore.Shared.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.ApiService.UnitTests.Infrastructure.Extensions;

public class ApplicationServicesExtensionsTests
{
    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_ShouldConfigureJwtClockSkewToThirtySeconds()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.ClockSkew).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithRs256Algorithm_ShouldConfigureRsaValidationKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "RS256",
                ["Jwt:SecretKey"] = string.Empty,
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RS256:PrivateKeyPem"] = privateKeyPem,
                ["Jwt:RS256:PublicKeyPem"] = publicKeyPem,
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<RsaSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithAlgorithmOmittedAndRs256KeysPresent_ShouldConfigureRsaValidationKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RS256:PrivateKeyPem"] = privateKeyPem,
                ["Jwt:RS256:PublicKeyPem"] = publicKeyPem,
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<RsaSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithAlgorithmOmittedAndRs256KeysAbsent_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithExplicitHs256AndRs256KeysPresent_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RS256:PrivateKeyPem"] = privateKeyPem,
                ["Jwt:RS256:PublicKeyPem"] = publicKeyPem,
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256SecretShorterThan32Bytes_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "too-short-secret",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act + Assert
        _ = await Assert.That(() => services.AddApplicationServices(configuration, environment))
            .Throws<InvalidOperationException>()
            .WithMessage("HS256 requires Jwt:SecretKey to be at least 32 bytes when UTF-8 encoded.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256SecretExactly32Utf8Bytes_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var thirtyTwoByteSecret = "Ab1!Ab1!Ab1!Ab1!Ab1!Ab1!Ab1!Ab1!";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = thirtyTwoByteSecret,
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256RepeatedCharacterSecretInProduction_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = new string('z', 32),
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        // Act + Assert
        _ = await Assert.That(() => services.AddApplicationServices(configuration, environment))
            .Throws<InvalidOperationException>()
            .WithMessage("HS256 Jwt:SecretKey is too weak for non-development environments: the key cannot be made of a single repeated character. Provide a high-entropy secret from a secure secret store, or prefer RS256 for production deployments.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256LowVarietySecretInProduction_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "aaabbbccaaabbbccaaabbbccaaabbbcc",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        // Act + Assert
        _ = await Assert.That(() => services.AddApplicationServices(configuration, environment))
            .Throws<InvalidOperationException>()
            .WithMessage("HS256 Jwt:SecretKey is too weak for non-development environments: the key must contain at least 4 distinct characters. Provide a high-entropy secret from a secure secret store, or prefer RS256 for production deployments.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256PlaceholderLikeSecretInProduction_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "YOUR SECRET KEY MUST BE AT LEAST 32 CHARACTERS LONG FOR HS256",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        // Act + Assert
        _ = await Assert.That(() => services.AddApplicationServices(configuration, environment))
            .Throws<InvalidOperationException>()
            .WithMessage("HS256 Jwt:SecretKey is too weak for non-development environments: the key matches a known placeholder/default value. Provide a high-entropy secret from a secure secret store, or prefer RS256 for production deployments.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256PlaceholderLikeSecretInDevelopment_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "your-secret-key-must-be-at-least-32-characters-long-for-hs256",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Development"
        };

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithHs256InProduction_ShouldConfigureSymmetricValidationKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Algorithm"] = "HS256",
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        _ = await Assert.That(jwtOptions.TokenValidationParameters.IssuerSigningKey).IsTypeOf<SymmetricSecurityKey>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithEmailDeliveryMethodNoneInDevelopment_ShouldAllowConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost",
                ["Email:DeliveryMethod"] = "None"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Development"
        };
        _ = services.AddSingleton<IConfiguration>(configuration);

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;

        // Assert
        _ = await Assert.That(options.DeliveryMethod).IsEqualTo("None");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithEmailDeliveryMethodNoneOutsideDevelopment_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost",
                ["Email:DeliveryMethod"] = "None"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };
        _ = services.AddSingleton<IConfiguration>(configuration);

        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        // Act + Assert
        _ = await Assert.That(() => provider.GetRequiredService<IOptions<EmailOptions>>().Value)
            .Throws<OptionsValidationException>()
            .WithMessage("Email:DeliveryMethod cannot be 'None' outside Development. Use 'Logging' or 'Smtp'.");
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithEmptyPasskeyAllowedOriginsOutsideDevelopment_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        _ = services.AddSingleton<IConfiguration>(configuration);
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        // Act + Assert
        _ = await Assert.That(() => provider.GetRequiredService<IOptions<PasskeyAllowedOriginsOptions>>().Value)
            .Throws<OptionsValidationException>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithEmptyPasskeyAllowedOriginsInDevelopment_ShouldAllowConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Development"
        };

        _ = services.AddSingleton<IConfiguration>(configuration);
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PasskeyAllowedOriginsOptions>>().Value;

        // Assert
        _ = await Assert.That(options.AllowedOrigins).IsEmpty();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithInvalidPasskeyAllowedOrigin_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost",
                ["Authentication:Passkey:AllowedOrigins:0"] = "https://localhost:7260/path"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        _ = services.AddSingleton<IConfiguration>(configuration);
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        // Act + Assert
        _ = await Assert.That(() => provider.GetRequiredService<IOptions<PasskeyAllowedOriginsOptions>>().Value)
            .Throws<OptionsValidationException>();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_WithPasskeyAllowedOriginTrailingSlash_ShouldNormalizeOrigin()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost",
                ["Authentication:Passkey:AllowedOrigins:0"] = "https://localhost:7260/"
            })
            .Build();

        var environment = new TestWebHostEnvironment
        {
            EnvironmentName = "Production"
        };

        _ = services.AddSingleton<IConfiguration>(configuration);
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<PasskeyAllowedOriginsOptions>>().Value;

        // Assert
        _ = await Assert.That(options.AllowedOrigins.Length).IsEqualTo(1);
        _ = await Assert.That(options.AllowedOrigins[0]).IsEqualTo("https://localhost:7260");
    }

    [Test]
    [Category("Unit")]
    public async Task OnTokenValidated_WhenUnexpectedExceptionOccurs_ShouldFailAuthAndClearUser()
    {
        // Arrange – build services WITHOUT Marten so the handler throws on IDocumentSession resolution
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();

        var environment = new TestWebHostEnvironment();
        _ = services.AddApplicationServices(configuration, environment);

        await using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            displayName: null,
            typeof(JwtBearerHandler));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString())],
                authenticationType: "Test"));
        var tokenValidatedContext = new TokenValidatedContext(httpContext, scheme, jwtOptions)
        {
            Principal = principal
        };

        // Act – IDocumentSession is not registered, so the handler body throws inside the try/catch
        await jwtOptions.Events.TokenValidated(tokenValidatedContext);

        // Assert – the catch block must have called context.Fail and cleared the principal
        _ = await Assert.That(tokenValidatedContext.Result?.Failure).IsNotNull();
        _ = await Assert.That(httpContext.User.Identity).IsNull();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_ShouldRegisterMaximumLengthPasswordValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();
        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IPasswordValidator<ApplicationUser>>();

        // Assert
        _ = await Assert.That(validators.Any(v => v is MaximumLengthPasswordValidator<ApplicationUser>)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task AddApplicationServices_ShouldConfigureIdentityRequiredLengthFromSharedPasswordValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-must-be-at-least-32-characters-long",
                ["Jwt:Issuer"] = "BookStore.ApiService",
                ["Jwt:Audience"] = "BookStore.Web",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Authentication:Passkey:ServerDomain"] = "localhost"
            })
            .Build();
        var environment = new TestWebHostEnvironment();

        // Act
        _ = services.AddApplicationServices(configuration, environment);
        await using var provider = services.BuildServiceProvider();
        var identityOptions = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        _ = await Assert.That(identityOptions.Password.RequiredLength).IsEqualTo(PasswordValidator.MinLength);
    }

    sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BookStore.ApiService.UnitTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
