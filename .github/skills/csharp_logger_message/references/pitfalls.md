# LoggerMessage Common Pitfalls

## 1. Forgetting `partial` on the class or method

The most common cause of "cannot implement partial method" or "partial method must have an implementation" errors.

```csharp
// ❌ Missing partial on the class
public static class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello {Name}")]
    public static partial void Hello(ILogger logger, string name);  // compile error
}

// ✅ Both class and method are partial
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello {Name}")]
    public static partial void Hello(ILogger logger, string name);
}
```

## 2. Putting Exception in the message template

The runtime captures the first `Exception` parameter automatically. Adding `{Ex}` or `{Exception}` to the template produces a `SYSLIB0025` warning and the exception appears twice.

```csharp
// ❌
[LoggerMessage(Level = LogLevel.Error, Message = "Failed: {Exception}")]
public static partial void Failed(ILogger logger, Exception exception);

// ✅
[LoggerMessage(Level = LogLevel.Error, Message = "Failed")]
public static partial void Failed(ILogger logger, Exception exception);
```

## 3. Using string interpolation in Message

The `Message` property is a *template*, not a format string. Interpolation is evaluated at compile time (producing a literal) or triggers a warning, and defeats structured logging.

```csharp
// ❌ Interpolated string loses structure
[LoggerMessage(Level = LogLevel.Information, Message = $"User {userId} logged in")]
// → placeholder values are baked in, no structured property captured

// ✅ Named placeholder
[LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} logged in")]
public static partial void UserLoggedIn(ILogger logger, Guid userId);
```

## 4. Exception not first after ILogger

The source generator treats the *first* `Exception` after `ILogger` specially. Putting it later breaks the implicit capture.

```csharp
// ❌ Exception at wrong position
[LoggerMessage(Level = LogLevel.Error, Message = "Failed for {OrderId}")]
public static partial void OrderFailed(ILogger logger, Guid orderId, Exception ex);
// → ex is treated as a template parameter, not captured by the runtime

// ✅ Exception immediately after ILogger
[LoggerMessage(Level = LogLevel.Error, Message = "Failed for {OrderId}")]
public static partial void OrderFailed(ILogger logger, Exception ex, Guid orderId);
```

## 5. Duplicate EventId

Reusing the same EventId in the same component confuses structured sinks. The source generator emits a warning for duplicates in the same partial class.

Assign EventId ranges per feature area and track them (see [basics.md](basics.md)).

## 6. Forgetting to call through the static class

After switching from extension methods to `[LoggerMessage]` it's tempting to keep writing `_logger.Log*`, which bypasses the source-generated method.

```csharp
// ❌ Bypasses source-generated method
_logger.LogInformation("User {UserId} updated", userId);

// ✅ Uses source-generated method
Log.Users.UserUpdated(_logger, userId);
// or, if using extension method style:
_logger.UserUpdated(userId);
```

## 7. Using non-partial return types

Only `void` is supported. Methods that return `bool`, `Task`, or anything else will not compile with `[LoggerMessage]`.

## 8. Mismatch between placeholder name and parameter name

The comparison is case-insensitive, but names must otherwise match. A typo means the parameter won't appear in the structured output.

```csharp
// ❌ Placeholder {BookdId} (typo) — parameter bookId won't be captured
[LoggerMessage(Level = LogLevel.Information, Message = "Looking up {BookdId}")]
public static partial void LookingUpBook(ILogger logger, Guid bookId);

// ✅ Consistent naming
[LoggerMessage(Level = LogLevel.Information, Message = "Looking up {BookId}")]
public static partial void LookingUpBook(ILogger logger, Guid bookId);
```
