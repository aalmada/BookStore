# Wolverine Sagas

Sagas (also called process managers) are **stateful, long-running workflows** that coordinate multiple messages over time. Wolverine persists saga state automatically using the configured storage (Marten, EF Core, SQL Server, etc.).

## Core Concepts

- Inherit from `Wolverine.Saga`
- State is loaded, mutated, and saved around every message handled by the saga
- `MarkCompleted()` signals Wolverine to delete the saga state after the current message
- A `static Start(...)` (or `static Handle(...)`) method creates the initial saga instance
- Instance methods named `Handle(...)` process subsequent messages on the existing instance
- Saga identity is resolved from `Id` property, `[SagaIdentity]` attribute, or a `{SagaTypeName}Id` property on the message

## Defining a Saga

```csharp
public record StartOrder(string OrderId);
public record CompleteOrder(string Id);

// Inherits TimeoutMessage — Wolverine schedules delivery after the given delay
public record OrderTimeout(string Id) : TimeoutMessage(1.Minutes());

public class Order : Saga
{
    public string? Id { get; set; }

    // Static Start creates and persists the new saga instance
    public static (Order, OrderTimeout) Start(StartOrder order, ILogger<Order> logger)
    {
        logger.LogInformation("Got a new order with id {Id}", order.OrderId);
        return (new Order { Id = order.OrderId }, new OrderTimeout(order.OrderId));
    }

    // Handle is called on the loaded saga instance
    public void Handle(CompleteOrder complete, ILogger<Order> logger)
    {
        logger.LogInformation("Completing order {Id}", complete.Id);
        MarkCompleted(); // saga state is deleted after this message
    }

    // Timeout handler — enforces a deadline
    public void Handle(OrderTimeout timeout, ILogger<Order> logger)
    {
        logger.LogInformation("Order {Id} timed out", timeout.Id);
        MarkCompleted();
    }

    // NotFound is called when a message arrives for a saga that no longer exists
    public static void NotFound(CompleteOrder complete, ILogger<Order> logger)
    {
        logger.LogInformation("Order {Id} not found", complete.Id);
    }
}
```

## Saga Identity Resolution (precedence order)

1. `[SagaIdentity]` attribute on a message property
2. Property named `{SagaType}Id` on the message (e.g., `OrderId` for an `Order` saga)
3. `Id` property on the message

```csharp
// Explicit attribute — highest precedence
public class SomeMessage
{
    [SagaIdentity] public Guid OrderId { get; set; }
}
```

## Timeouts

Define timeouts using `TimeoutMessage`:

```csharp
public record OrderTimeout(string Id) : TimeoutMessage(1.Minutes());
```

Return the timeout alongside the saga from `Start` (or any handler) and Wolverine schedules it automatically:

```csharp
public static (Order, OrderTimeout) Start(StartOrder order) =>
    (new Order { Id = order.OrderId }, new OrderTimeout(order.OrderId));
```

## Cascading Messages from Sagas

Handlers may return messages (cascading) to trigger downstream steps:

```csharp
public object[] Handle(CreditReserved creditReserved, ILogger logger)
{
    OrderStatus = OrderStatus.CreditReserved;
    return [new ApproveOrder(creditReserved.OrderId, creditReserved.CustomerId)];
}
```

## Starting a Saga from an External Handler

A saga can be started from outside the saga class by returning the saga instance from a regular handler:

```csharp
public class StartReservationHandler
{
    public static (ReservationBooked, Reservation, ReservationTimeout) Handle(StartReservation start) =>
        (new ReservationBooked(start.ReservationId, DateTimeOffset.UtcNow),
         new Reservation { Id = start.ReservationId },
         new ReservationTimeout(start.ReservationId));
}
```

## Starting a Saga from an HTTP Endpoint

```csharp
[WolverinePost("/reservation")]
public static (ReservationBooked, Reservation, ReservationTimeout) Post(StartReservation start) =>
    (new ReservationBooked(start.ReservationId, DateTimeOffset.UtcNow),
     new Reservation { Id = start.ReservationId },
     new ReservationTimeout(start.ReservationId));
```

## Multiple Sagas for the Same Message

By default Wolverine uses a single handler per message. To allow multiple independent sagas to react to the same message, configure `MultipleHandlerBehavior.Separated`:

```csharp
builder.UseWolverine(opts =>
{
    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
});
```

```csharp
public record OrderPlaced(Guid OrderPlacedId, string ProductName);

public class ShippingSaga : Saga
{
    public Guid Id { get; set; }
    public static ShippingSaga Start(OrderPlaced message) => new() { Id = message.OrderPlacedId };
    public void Handle(OrderShipped message) => MarkCompleted();
}

public class BillingSaga : Saga
{
    public Guid Id { get; set; }
    public static BillingSaga Start(OrderPlaced message) => new() { Id = message.OrderPlacedId };
    public void Handle(PaymentReceived message) => MarkCompleted();
}
```

## Resequencer Saga

Use `ResequencerSaga<T>` to process messages in a guaranteed order:

```csharp
public interface SequencedMessage { int? Order { get; } }
public record MySequencedCommand(Guid SagaId, int? Order) : SequencedMessage;

public class MyWorkflowSaga : ResequencerSaga<MySequencedCommand>
{
    public Guid Id { get; set; }
    public static MyWorkflowSaga Start(StartMyWorkflow cmd) => new() { Id = cmd.Id };
    public void Handle(MySequencedCommand cmd) { /* process in order */ }
}
```

## Marten-Backed Sagas

With Marten integration (`WolverineFx.Marten`), saga state is stored as a Marten document. No additional configuration is required — Marten is auto-detected as the storage backend.

Strong-typed identifiers are supported:

```csharp
[StronglyTypedId(Template.Guid)]
public readonly partial struct OrderSagaId;

public class OrderSagaWorkflow : Saga
{
    public OrderSagaId Id { get; set; }
    public bool ItemsPicked { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool Shipped { get; set; }

    public static OrderSagaWorkflow Start(StartOrderSaga command) =>
        new() { Id = command.OrderId };

    public void Handle(PickOrderItems command)
    {
        ItemsPicked = true;
        CheckForCompletion();
    }

    public void Handle(ProcessOrderPayment command)
    {
        PaymentProcessed = true;
        CheckForCompletion();
    }

    public void Handle(ShipOrder command)
    {
        Shipped = true;
        CheckForCompletion();
    }

    public void Handle(CancelOrderSaga command) => MarkCompleted();

    private void CheckForCompletion()
    {
        if (ItemsPicked && PaymentProcessed && Shipped)
            MarkCompleted();
    }
}
```

## Common Mistakes

| Mistake | Fix |
|---|---|
| No `Id` property or `[SagaIdentity]` on messages | Add an `Id` / `{SagaType}Id` property or `[SagaIdentity]` |
| Missing `NotFound` handler | Messages for deleted sagas throw; add `static void NotFound(...)` |
| Timeout not returned from `Start` | Return the `TimeoutMessage` alongside the saga instance |
| Multiple sagas for same message silently ignored | Set `MultipleHandlerBehavior.Separated` |
| Calling `SaveChangesAsync()` manually | Let Wolverine manage the transaction — never call it in saga handlers |

See also: [wolverine-advanced.md](wolverine-advanced.md), [wolverine-marten.md](wolverine-marten.md)
