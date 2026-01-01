using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Projections;

public class AuthorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Biography { get; set; } = string.Empty; // Localized
    public string ProjectionLocale { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}

public class AuthorProjectionBuilder : EventProjection
{
    private readonly LocalizationOptions _localization;

    public AuthorProjectionBuilder(IOptions<LocalizationOptions> localizationOptions)
        => _localization = localizationOptions.Value;

    public async Task Project(IEvent<AuthorAdded> @event, IDocumentOperations ops)
    {
        var author = @event.Data;
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                using var tenantSession = session.ForTenant(culture);

                var projection = new AuthorProjection
                {
                    Id = author.Id,
                    Name = author.Name,
                    Biography = GetLocalizedBio(author.Translations, culture),
                    ProjectionLocale = culture,
                    LastModified = author.Timestamp
                };
                tenantSession.Store(projection);
                // await tenantSession.SaveChangesAsync(); // Auto-saved by parent session batch
            }
        }
    }

    public async Task Project(IEvent<AuthorUpdated> @event, IDocumentOperations ops)
    {
        var author = @event.Data;
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                using var tenantSession = session.ForTenant(culture);

                var projection = new AuthorProjection
                {
                    Id = author.Id,
                    Name = author.Name,
                    Biography = GetLocalizedBio(author.Translations, culture),
                    ProjectionLocale = culture,
                    LastModified = author.Timestamp
                };
                tenantSession.Store(projection);
                // await tenantSession.SaveChangesAsync();
            }
        }
    }

    public async Task Project(IEvent<AuthorSoftDeleted> @event, IDocumentOperations ops)
    {
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                using var tenantSession = session.ForTenant(culture);
                tenantSession.DeleteWhere<AuthorProjection>(x => x.Id == @event.Data.Id);
                // await tenantSession.SaveChangesAsync();
            }
        }
    }

    private string GetLocalizedBio(Dictionary<string, AuthorTranslation>? translations, string culture)
    {
        if (translations == null || translations.Count == 0)
        {
            return string.Empty;
        }

        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

        // 1. Exact
        if (translations.TryGetValue(culture, out var exact))
        {
            return exact.Biography ?? string.Empty;
        }

        // 2. Neutral
        if (translations.TryGetValue(cultureInfo.TwoLetterISOLanguageName, out var neutral))
        {
            return neutral.Biography ?? string.Empty;
        }

        // 3. Default Culture
        var defaultCulture = _localization.DefaultCulture;
        if (translations.TryGetValue(defaultCulture, out var def))
        {
            return def.Biography ?? string.Empty;
        }

        // 4. Default Neutral
        var defaultNeutral = System.Globalization.CultureInfo.GetCultureInfo(defaultCulture).TwoLetterISOLanguageName;
        if (translations.TryGetValue(defaultNeutral, out var defNeutral))
        {
            return defNeutral.Biography ?? string.Empty;
        }

        // 5. Fallback
        return string.Empty;
    }
}
