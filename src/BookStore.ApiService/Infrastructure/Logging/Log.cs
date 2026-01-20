using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Central logging class using source-generated log messages for high-performance, structured logging.
/// Organized by feature area using partial classes for maintainability.
/// </summary>
public static partial class Log
{
    // Nested partial classes for feature areas
    public static partial class Books { }
    public static partial class Authors { }
    public static partial class Categories { }
    public static partial class Publishers { }
    public static partial class Infrastructure { }
    public static partial class Email { }
    public static partial class Users { }
    public static partial class Tenants { }
}
