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
        public const string Author = "M12 12c2.76 0 5-2.24 5-5S14.76 2 12 2 7 4.24 7 7s2.24 5 5 5Zm0 2c-3.31 0-10 1.66-10 5v3h20v-3c0-3.34-6.69-5-10-5Z";
        public const string Category = "M3 5h8v6H3V5Zm10 0h8v6h-8V5ZM3 13h8v6H3v-6Zm10 0h8v6h-8v-6Z";
        public const string Publisher = "M3 22h18v-2H3v2Zm2-4h3V8H5v10Zm5 0h4V4h-4v14Zm6 0h3v-7h-3v7Z";
        public const string Book = "M4 5a3 3 0 0 1 3-3h13v18H7a3 3 0 0 0-3 3V5Zm4 0h8v2H8V5Zm0 4h8v2H8V9Z";
        public const string Favorite = "M12 21.35 10.55 20.03C5.4 15.36 2 12.28 2 8.5A5.5 5.5 0 0 1 7.5 3c1.74 0 3.41.81 4.5 2.09A5.98 5.98 0 0 1 16.5 3 5.5 5.5 0 0 1 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35Z";
        public const string FavoriteBorder = "M16.5 3A5.98 5.98 0 0 0 12 5.09 5.98 5.98 0 0 0 7.5 3 5.5 5.5 0 0 0 2 8.5c0 3.78 3.4 6.86 8.55 11.54L12 21.35l1.45-1.32C18.6 15.36 22 12.28 22 8.5A5.5 5.5 0 0 0 16.5 3Z";
        public const string Star = "m12 17.27 6.18 3.73 1.64-7.03L14 9.24 6.81 8.63 12 2l5.19 6.63 7.19.61-5.82 4.73 1.64 7.03L12 17.27Z";
        public const string StarBorder = "M22 9.24 14.81 8.63 12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21 12 17.27 18.18 21l-1.64-7.03L22 9.24Zm-10 5.03-3.76 2.27 1-4.28L5.91 9.4l4.38-.38L12 5.1l1.71 3.92 4.38.38-3.33 2.86 1 4.28L12 14.27Z";
        public const string StarHalf = "M22 9.24 14.81 8.63 12 2v12.27l6.18 3.73-1.64-7.03L22 9.24Z";
    }
}
