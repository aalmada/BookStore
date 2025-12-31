using BookStore.ApiService.Events;
using BookStore.ApiService.Models;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Microsoft.Extensions.Options;
using JasperFx.Events;

namespace BookStore.ApiService.Projections;

public class CategoryProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Localized
    public string ProjectionLocale { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}

public class CategoryProjectionBuilder : EventProjection
{
    private readonly LocalizationOptions _localization;

    public CategoryProjectionBuilder(IOptions<LocalizationOptions> localizationOptions)
    {
        _localization = localizationOptions.Value;
    }
    
    public async Task Project(IEvent<CategoryAdded> @event, IDocumentOperations ops)
    {
         var category = @event.Data;
         if (ops is IDocumentSession session)
         {
             foreach (var culture in _localization.SupportedCultures)
             {
                 using var tenantSession = session.ForTenant(culture);
                 
                 var projection = new CategoryProjection
                 {
                     Id = category.Id,
                     Name = GetLocalizedName(category.Translations, culture),
                     ProjectionLocale = culture,
                     LastModified = category.Timestamp
                 };
                 tenantSession.Store(projection);
                 // await tenantSession.SaveChangesAsync();
             }
         }
    }

    public async Task Project(IEvent<CategoryUpdated> @event, IDocumentOperations ops)
    {
         var category = @event.Data;
         if (ops is IDocumentSession session)
         {
             foreach (var culture in _localization.SupportedCultures)
             {
                 using var tenantSession = session.ForTenant(culture);
                 
                 var projection = new CategoryProjection
                 {
                     Id = category.Id,
                     Name = GetLocalizedName(category.Translations, culture),
                     ProjectionLocale = culture,
                     LastModified = category.Timestamp
                 };
                 tenantSession.Store(projection);
                 // await tenantSession.SaveChangesAsync();
             }
         }
    }
    
    public async Task Project(IEvent<CategorySoftDeleted> @event, IDocumentOperations ops)
    {
        if (ops is IDocumentSession session)
        {
            foreach (var culture in _localization.SupportedCultures)
            {
                 using var tenantSession = session.ForTenant(culture);
                 tenantSession.DeleteWhere<CategoryProjection>(x => x.Id == @event.Data.Id);
                 // await tenantSession.SaveChangesAsync();
            }
        }
    }
    
    private string GetLocalizedName(Dictionary<string, CategoryTranslation>? translations, string culture)
    {
        if (translations == null || translations.Count == 0) return "Unknown";

        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

        // 1. Exact
        if (translations.TryGetValue(culture, out var exact)) return exact.Name;
        
        // 2. Neutral
        if (translations.TryGetValue(cultureInfo.TwoLetterISOLanguageName, out var neutral)) return neutral.Name;
        
        // 3. Default Culture
        var defaultCulture = _localization.DefaultCulture;
        if (translations.TryGetValue(defaultCulture, out var def)) return def.Name;
        
        // 4. Default Neutral
        var defaultNeutral = System.Globalization.CultureInfo.GetCultureInfo(defaultCulture).TwoLetterISOLanguageName;
        if (translations.TryGetValue(defaultNeutral, out var defNeutral)) return defNeutral.Name;
        
        // 5. Fallback (First available)
        return translations.Values.FirstOrDefault()?.Name ?? "Unknown";
    }
}
