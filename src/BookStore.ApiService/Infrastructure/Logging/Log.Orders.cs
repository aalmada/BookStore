using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

public static partial class Log
{
    public static partial class Orders
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Order placed: OrderId={OrderId}, Email={CustomerEmail}")]
        public static partial void OrderPlaced(ILogger logger, Guid orderId, string customerEmail);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Order validation failed: OrderId={OrderId}, Reason={Reason}")]
        public static partial void OrderValidationFailed(ILogger logger, Guid orderId, string reason);
    }
}
