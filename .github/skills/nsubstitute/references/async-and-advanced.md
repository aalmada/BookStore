# NSubstitute — Async, Exceptions, Callbacks, Events

## Async methods (Task / Task<T> / ValueTask)

NSubstitute handles async methods transparently — `.Returns()` accepts both synchronous values and Tasks.

```csharp
// Stub returning a value
_ = service.GetUserAsync(id).Returns(user);          // auto-wrapped in Task

// Stub returning Task.CompletedTask (void async)
_ = service.SendAsync(email).Returns(Task.CompletedTask);

// Stub returning null (explicit generic needed to avoid CS0121)
_ = service.FindAsync(id).Returns((User?)null);

// Throw inside a Returns callback — must specify the type explicitly (CS0121 workaround)
_ = service.GetAsync(id).Returns<User>(x => { throw new NotFoundException(); });

// Verify async call
await service.Received(1).GetUserAsync(id);
```

**CS0121 tip**: When a method returns `Task<T>` and you pass a lambda that throws, the compiler can't resolve which `Returns` overload to use. Write `Returns<T>(x => { throw ... })` with the explicit generic parameter.

---

## Throwing exceptions

```csharp
// Synchronous throw on method call
service.When(x => x.Process(Arg.Any<Order>()))
       .Do(x => { throw new InvalidOperationException("failed"); });

// Or using Throws extension (requires NSubstitute.Extensions or direct When/Do)
service.When(x => x.Delete(Arg.Any<Guid>()))
       .Throw(new UnauthorizedException());

// Throw on async method
_ = service.GetAsync(id).Returns<User>(_ => throw new NotFoundException());
```

---

## Callbacks and side effects

Use callbacks when you need side effects beyond a return value — for example, capturing arguments, simulating state mutations, or tracking invocation counts.

```csharp
// Capture argument via Arg.Do
var sentEmails = new List<string>();
_ = emailService.SendAsync(Arg.Do<string>(addr => sentEmails.Add(addr)))
                .Returns(Task.CompletedTask);

// Run code on every call using When...Do (good for void methods)
var callCount = 0;
service.When(x => x.Notify(Arg.Any<string>()))
       .Do(x => callCount++);

// AndDoes — side effect alongside a return value
_ = service.Process(Arg.Any<Order>())
           .Returns(Result.Ok())
           .AndDoes(x => Console.WriteLine($"Processed {x.Arg<Order>().Id}"));

// Callback builder for sequenced behaviour
service.When(x => x.Run())
       .Do(Callback
           .First(x => Console.WriteLine("first call"))
           .Then(x => Console.WriteLine("second call"))
           .ThenKeepDoing(x => Console.WriteLine("subsequent")));
```

**When to use `When...Do` vs `Returns + AndDoes`**: prefer `Returns + AndDoes` for non-void methods (cleaner). Use `When...Do` for `void` methods or when you need complex sequenced behaviour via `Callback`.

---

## Events

```csharp
// Subscribe and raise event
var command = Substitute.For<ICommand>();
command.Executed += Raise.Event();                        // EventHandler
command.DataReceived += Raise.Event<DataEventArgs>(args); // EventHandler<T>

// Verify subscription
command.Received().Executed += Arg.Any<EventHandler>();
```

---

## Partial substitutes

Use `ForPartsOf<T>` when you want real behaviour for most methods but need to stub one dependency-heavy or side-effectful method.

```csharp
var reader = Substitute.ForPartsOf<FileReader>();

// MUST call Configure() first — otherwise the real method runs during setup
reader.Configure().ReadFile("data.csv").Returns("1,2,3");

// Now the real Read() will call the stubbed ReadFile()
var result = reader.Read("data.csv");
```

Only works for `virtual` methods. Non-virtual code always runs as-is; use NSubstitute.Analyzers to catch this at compile time.
