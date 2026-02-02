namespace BookStore.AppHost.Tests;

public static class TestConstants
{
    /// <summary>
    /// Default timeout for waiting for SSE events or other async operations.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default timeout for waiting for specific SSE events (shorter than global default).
    /// </summary>
    public static readonly TimeSpan DefaultEventTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default delay to allow for asynchronous projections to complete.
    /// </summary>
    public static readonly TimeSpan DefaultProjectionDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Default delay between retries.
    /// </summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default polling interval for checking recurring conditions.
    /// </summary>
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Default timeout for SSE streams to prevent premature closure.
    /// </summary>
    public static readonly TimeSpan DefaultStreamTimeout = TimeSpan.FromMinutes(5);
}
