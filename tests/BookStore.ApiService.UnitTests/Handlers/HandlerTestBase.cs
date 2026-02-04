using BookStore.ApiService.Infrastructure;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BookStore.ApiService.UnitTests.Handlers;

/// <summary>
/// Base class for all ApiService command handler unit tests.
/// Centralizes the setup of common mocks and configuration options.
/// </summary>
public abstract class HandlerTestBase
{
    protected IDocumentSession Session { get; } = Substitute.For<IDocumentSession>();
    protected IHttpContextAccessor HttpContextAccessor { get; } = Substitute.For<IHttpContextAccessor>();
    protected HybridCache Cache { get; } = Substitute.For<HybridCache>();
    protected ILogger Logger { get; } = Substitute.For<ILogger>();
    protected ILogger<T> GetLogger<T>() => Substitute.For<ILogger<T>>();
    protected IOptions<LocalizationOptions> LocalizationOptions { get; }
    protected IOptions<CurrencyOptions> CurrencyOptions { get; }

    protected HandlerTestBase()
    {
        LocalizationOptions = Options.Create(new LocalizationOptions
        {
            DefaultCulture = "en",
            SupportedCultures = ["en"]
        });

        CurrencyOptions = Options.Create(new CurrencyOptions
        {
            DefaultCurrency = "USD",
            SupportedCurrencies = ["USD", "EUR"]
        });

        var httpContext = new DefaultHttpContext();
        _ = HttpContextAccessor.HttpContext.Returns(httpContext);
        _ = Session.CorrelationId.Returns("test-correlation-id");
    }
}
