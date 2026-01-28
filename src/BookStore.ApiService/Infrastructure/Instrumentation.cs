using System.Diagnostics.Metrics;

namespace BookStore.ApiService.Infrastructure;

public static class Instrumentation
{
    public const string MeterName = "BookStore.ApiService";
    private static readonly Meter Meter = new(MeterName);

    // Book Interactions
    public static readonly Counter<long> BookViews = Meter.CreateCounter<long>("bookstore.books.views", description: "Number of times book profiles are viewed");
    public static readonly Counter<long> BookSearches = Meter.CreateCounter<long>("bookstore.books.searches", description: "Number of searches performed");
    public static readonly Histogram<double> SearchDuration = Meter.CreateHistogram<double>("bookstore.books.search_duration", unit: "ms", description: "Latency of search queries");

    // User Lifestyle & Cart
    public static readonly Counter<long> CartAdded = Meter.CreateCounter<long>("bookstore.users.cart.added", description: "Items added to the shopping cart");
    public static readonly Counter<long> CartRemoved = Meter.CreateCounter<long>("bookstore.users.cart.removed", description: "Items removed from the cart");
    public static readonly Counter<long> FavoritesAdded = Meter.CreateCounter<long>("bookstore.users.favorites.added", description: "Books added to favorites");
    public static readonly Counter<long> RatingsAdded = Meter.CreateCounter<long>("bookstore.users.ratings.added", description: "Book ratings submitted");

    // Sales Management
    public static readonly Counter<long> SalesScheduled = Meter.CreateCounter<long>("bookstore.sales.scheduled", description: "New sales scheduled");
    public static readonly Counter<long> SalesCanceled = Meter.CreateCounter<long>("bookstore.sales.canceled", description: "Canceled sales");
}
