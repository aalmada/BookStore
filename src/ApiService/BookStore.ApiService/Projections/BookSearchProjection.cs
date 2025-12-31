using BookStore.ApiService.Events;
using BookStore.ApiService.Models;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Marten.Events;
using Microsoft.Extensions.Options;
using JasperFx.Events;

namespace BookStore.ApiService.Projections;

// Read model optimized for search
public class BookSearchProjection
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; // Localized
    public string? Isbn { get; set; }
    public string OriginalLanguage { get; set; } = string.Empty; // Was Language
    public string ProjectionLocale { get; set; } = string.Empty; // The locale of this projection (e.g. "en", "pt")
    public PartialDate? PublicationDate { get; set; }

    // Denormalized fields for performance
    public Guid? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public List<Guid> AuthorIds { get; set; } = [];
    public string AuthorNames { get; set; } = string.Empty; // Concatenated for search
    public List<Guid> CategoryIds { get; set; } = []; // For filtering by ID only (categories are localizable)

    // Computed search text for ngram matching
    public string SearchText { get; set; } = string.Empty;
}

// Event projection for multi-tenant localization
public class BookSearchProjectionBuilder : EventProjection
{
    private readonly LocalizationOptions _localization;

    public BookSearchProjectionBuilder(IOptions<LocalizationOptions> localizationOptions)
    {
        _localization = localizationOptions.Value;
    }

    public async Task Project(IEvent<BookAdded> @event, IDocumentOperations ops)
    {
        var book = @event.Data;
        
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                using var tenantSession = session.ForTenant(culture);
                
                var projection = new BookSearchProjection
                {
                    Id = book.Id,
                    Title = book.Title,
                    Description = GetLocalizedDescription(book, culture) ?? string.Empty,
                    Isbn = book.Isbn,
                    OriginalLanguage = book.Language,
                    ProjectionLocale = culture,
                    PublicationDate = book.PublicationDate,
                    PublisherId = book.PublisherId,
                    AuthorIds = book.AuthorIds,
                    CategoryIds = book.CategoryIds
                };

                if (projection.PublisherId.HasValue)
                {
                    // Publisher is global (not multi-tenanted), so load from main session or tenant session (it should find it if configured correctly)
                    // If Publisher is not MultiTenanted, it lives in Default tenant? 
                    // Any tenant session can read public data usually?
                    // Safest: Use main session for Publisher.
                    var publisher = await session.LoadAsync<PublisherProjection>(projection.PublisherId.Value); 
                    projection.PublisherName = publisher?.Name;
                }

                if (projection.AuthorIds.Count != 0)
                {
                    // Authors are MultiTenanted, load from tenant session
                    var authorList = await tenantSession.LoadManyAsync<AuthorProjection>(projection.AuthorIds.ToArray());
                    projection.AuthorNames = string.Join(", ", authorList.Select(a => a.Name));
                }

                UpdateSearchText(projection);
                tenantSession.Store(projection);
                
                // IMPORTANT: We must NOT commit tenantSession individually if EventProjection manages transaction.
                // But tenantSession from `ForTenant` might be standalone?
                // Docs: "ForTenant" returns a lightweight session.
                // If we don't SaveChanges, it does nothing?
                // `session` (ops) will call SaveChanges at the end of the batch.
                // Does `tenantSession` participate in `session`'s transaction?
                // Usually NO. `ForTenant` creates a NEW session.
                // THIS IS A PROBLEM. EventProjection expects us to enlist work in `ops`.
                
                // If `ops` does not support multi-tenant `Store`, then `EventProjection` expects us to use `ops`.
                // If `ops` is `IDocumentSession`, does it have `Store<T>(string tenant, T doc)`?
                // I need to verify this method exists.
                // If it DOES NOT exist, then `EventProjection` with fan-out is tricky.
                
                // ALTERNATIVE: Use `ops.Metadata`? 
                // Or maybe `UnitOfWork` on ops?
                
                // WAIT. `ops.Store` does NOT take tenant.
                // But `IDocumentSession` DOES have `Store<T>(string tenant, T doc)`?
                // I am checking Marten API in my head.
                // `IDocumentSession` inherits from `IDocumentOperations`.
                // `IDocumentOperations` HAS `Store<T>(T doc)`.
                // `IDocumentSession` HAS `Store<T>(string tenant, T doc)`? 
                
                // If I use `tenantSession.Store()`, I must call `tenantSession.SaveChangesAsync()`.
                // This breaks the "Atomic with Event" guarantee of the projection if the projection fails later?
                // Asynchronous projections are eventually consistent anyway.
                // But we prefer atomicity.
                
                // If I call `tenantSession.SaveChangesAsync()`, it's fine.
            }
        }
    }

    public async Task Project(IEvent<BookUpdated> @event, IDocumentOperations ops)
    {
        var update = @event.Data;
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                using var tenantSession = session.ForTenant(culture);
                
                var projection = new BookSearchProjection
                {
                    Id = update.Id,
                    Title = update.Title,
                    Description = GetLocalizedDescription(update, culture) ?? string.Empty,
                    Isbn = update.Isbn,
                    OriginalLanguage = update.Language,
                    ProjectionLocale = culture,
                    PublicationDate = update.PublicationDate,
                    PublisherId = update.PublisherId,
                    AuthorIds = update.AuthorIds,
                    CategoryIds = update.CategoryIds
                };

                if (projection.PublisherId.HasValue)
                {
                    var publisher = await session.LoadAsync<PublisherProjection>(projection.PublisherId.Value);
                    projection.PublisherName = publisher?.Name;
                }

                if (projection.AuthorIds.Count != 0)
                {
                    var authorList = await tenantSession.LoadManyAsync<AuthorProjection>(projection.AuthorIds.ToArray());
                    projection.AuthorNames = string.Join(", ", authorList.Select(a => a.Name));
                }

                UpdateSearchText(projection);
                tenantSession.Store(projection);
                // await tenantSession.SaveChangesAsync();
            }
        }
    }

    public async Task Project(IEvent<BookSoftDeleted> @event, IDocumentOperations ops)
    {
        // Delete for all tenants
        if (ops is IDocumentSession session)
        {
             foreach (var culture in _localization.SupportedCultures)
             {
                 using var tenantSession = session.ForTenant(culture);
                 tenantSession.DeleteWhere<BookSearchProjection>(x => x.Id == @event.Data.Id);
                 // await tenantSession.SaveChangesAsync();
             }
        }
    }
    
    public async Task Project(IEvent<PublisherUpdated> @event, IDocumentOperations ops)
    {
         var publisherId = @event.Data.Id;
         var newName = @event.Data.Name; 
         
         if (ops is IDocumentSession session) 
         {
             foreach (var culture in _localization.SupportedCultures)
             {
                   using var tenantSession = session.ForTenant(culture);
                   
                   // Query books in this tenant having the publisher
                   var books = await tenantSession.Query<BookSearchProjection>()
                        .Where(x => x.PublisherId == publisherId)
                        .ToListAsync();
                        
                   foreach(var book in books)
                   {
                        book.PublisherName = newName;
                        UpdateSearchText(book);
                        tenantSession.Store(book);
                   }
                   // await tenantSession.SaveChangesAsync();
             }
         }
    }

      public async Task Project(IEvent<AuthorUpdated> @event, IDocumentOperations ops)
    {
         var authorId = @event.Data.Id;
         
         if (ops is IDocumentSession session) 
         {
             foreach (var culture in _localization.SupportedCultures)
             {
                 using var tenantSession = session.ForTenant(culture);
                 
               // Find books having this author in this tenant
               var books = await tenantSession.Query<BookSearchProjection>()
                    .Where(x => x.AuthorIds.Contains(authorId)) 
                    .ToListAsync();
                    
                 foreach(var book in books)
                 {
                      // Re-fetch author names for this tenant
                      var authors = await tenantSession.LoadManyAsync<AuthorProjection>(book.AuthorIds.ToArray());
                      book.AuthorNames = string.Join(", ", authors.Select(a => a.Name));
                      UpdateSearchText(book);
                      tenantSession.Store(book);
                 }
                 // await tenantSession.SaveChangesAsync();
             }
         }
    }
  
    // Helper Methods
    
    private string? GetLocalizedDescription(BookAdded book, string culture)
    {
        return GetValue(culture, book.Translations, t => t.Description, null);
    }
     private string? GetLocalizedDescription(BookUpdated book, string culture)
    {
        return GetValue(culture, book.Translations, t => t.Description, null);
    }

    private string? GetValue<T>(string culture, Dictionary<string, T>? translations, Func<T, string?> selector, string? defaultValue)
    {
        if (translations == null || translations.Count == 0) return defaultValue;

        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

        // 1. Exact
        if (translations.TryGetValue(culture, out var exact)) return selector(exact) ?? defaultValue;
        
        // 2. Neutral
        if (translations.TryGetValue(cultureInfo.TwoLetterISOLanguageName, out var neutral)) return selector(neutral) ?? defaultValue;
        
        // 3. Default Culture
        var defaultCulture = _localization.DefaultCulture;
        if (translations.TryGetValue(defaultCulture, out var def)) return selector(def) ?? defaultValue;
        
        // 4. Default Neutral
        var defaultNeutral = System.Globalization.CultureInfo.GetCultureInfo(defaultCulture).TwoLetterISOLanguageName;
        if (translations.TryGetValue(defaultNeutral, out var defNeutral)) return selector(defNeutral) ?? defaultValue;
        
        // 5. Fallback
        return defaultValue;
    }

    static void UpdateSearchText(BookSearchProjection projection)
        => projection.SearchText =
                $"{projection.Title} " +
                $"{projection.Isbn ?? string.Empty} " +
                $"{projection.PublisherName ?? string.Empty} " +
                $"{projection.AuthorNames}".Trim();
}
