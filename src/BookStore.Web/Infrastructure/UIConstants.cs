namespace BookStore.Web.Infrastructure;

public static class UIConstants
{
    public const int DefaultPageSize = 10;
    public const int LargePageSize = 50;
    public const int MaxPageSize = 1000;

    public const int DebounceDelay = 500;
    public const int SnackbarDuration = 3000;

    public static class Icons
    {
        public const string Author = MudBlazor.Icons.Material.Filled.Person;
        public const string Category = MudBlazor.Icons.Material.Filled.Category;
        public const string Publisher = MudBlazor.Icons.Material.Filled.Business;
        public const string Book = MudBlazor.Icons.Material.Filled.MenuBook;
        public const string Favorite = MudBlazor.Icons.Material.Filled.Favorite;
        public const string FavoriteBorder = MudBlazor.Icons.Material.Filled.FavoriteBorder;
        public const string Star = MudBlazor.Icons.Material.Filled.Star;
        public const string StarBorder = MudBlazor.Icons.Material.Outlined.StarBorder;
        public const string StarHalf = MudBlazor.Icons.Material.Filled.StarHalf;
    }
}
