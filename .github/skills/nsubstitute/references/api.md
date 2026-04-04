# NSubstitute Core API Reference

## Creating substitutes

```csharp
// Interface (recommended)
var service = Substitute.For<IEmailService>();

// Multiple interfaces
var cmd = Substitute.For<ICommand, IDisposable>();

// Class (only virtual members are intercepted — use with care)
var reader = Substitute.For<SomReader>("ctor-arg");

// Delegate
var transform = Substitute.For<Func<string, int>>();
transform("hello").Returns(42);

// Partial substitute (real methods run unless configured)
var partial = Substitute.ForPartsOf<MyClass>();
partial.Configure().SomeVirtual().Returns("stubbed"); // use Configure() to avoid running real code
```

The `Substitute.ForPartsOf<T>()` trap: calling `partial.SomeVirtual().Returns(...)` without `Configure()` **actually invokes the real method** during setup, causing side effects. Always use `partial.Configure().Method()` first.

---

## Setting return values

```csharp
// Fixed value
_ = service.GetUserAsync(userId).Returns(user);

// Value based on arguments (CallInfo)
_ = service.GetAsync(default).ReturnsForAnyArgs(x => new User { Id = x.Arg<Guid>() });

// Sequence of values (each call gets the next)
_ = cache.Get<string>("key").Returns("first", "second", "third");

// Sequence with exceptions
_ = calculator.Mode.Returns("DEC", "HEX", x => { throw new Exception("exhausted"); });

// Property
_ = httpContextAccessor.HttpContext.Returns(new DefaultHttpContext());
```

`Returns(value)` is argument-specific: it only matches calls with those exact arguments. Use `ReturnsForAnyArgs(value)` (or `Arg.Any<T>()` matchers) when you don't care which arguments are passed.

---

## Argument matchers

Matchers are valid **only inside Return/Received/When.Do** calls — never in real production calls.

```csharp
// Match anything
service.Send(Arg.Any<string>()).Returns(true);

// Match with predicate
service.Send(Arg.Is<string>(s => s.StartsWith("admin"))).Returns(false);

// Capture argument for inspection
var captured = default(string);
service.Send(Arg.Do<string>(x => captured = x)).Returns(true);

// out parameters
lookup.TryLookup("key", out Arg.Any<string>())
      .Returns(x => { x[1] = "value"; return true; });
```

---

## Verifying calls

```csharp
// Received exactly N times (default = once or more if no arg given)
service.Received(1).Send("hello@example.com");

// Received any number of times
service.Received().Send(Arg.Any<string>());

// Not received
service.DidNotReceive().Send("admin@example.com");

// Received with any args (ignores argument values entirely)
service.ReceivedWithAnyArgs().Send(default);

// Property getter / setter
_ = cache.Received().Get<User>("key");
cache.Received().Size = Arg.Is<int>(n => n > 0);

// Indexers
dictionary.Received()["key"] = Arg.Is<int>(v => v > 0);
```

The `Received()` chain returns a proxy — access the member on it to declare what you expected. This is purely declarative; the framework inspects the recorded calls and throws `ReceivedCallsException` if the expectation isn't met.

---

## Clearing and resetting

```csharp
// Clear recorded calls (but keep stubs)
sub.ClearReceivedCalls();

// Clear both calls and stubs
sub.ClearSubstitute(ClearOptions.All);
```
