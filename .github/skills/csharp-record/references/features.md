# Record Features: Value Equality, `with`, `ToString`, and `Deconstruct`

## Value equality

Records compare by value, not by reference. Two independent instances with the same data are equal.

```csharp
public record Point(double X, double Y);

var a = new Point(1, 2);
var b = new Point(1, 2);

Console.WriteLine(a == b);              // True
Console.WriteLine(a.Equals(b));         // True
Console.WriteLine(ReferenceEquals(a, b)); // False — different objects
```

The compiler synthesises:
- `Equals(object? obj)` override
- `Equals(T? other)` strongly typed overload (`IEquatable<T>`)
- `operator ==` and `operator !=`
- `GetHashCode()` derived from all public properties

### Shallow equality on reference properties

Value equality compares references for reference-type properties, not their content. Two records pointing to *different* list instances with the *same* items are **not** equal.

```csharp
public record Order(List<string> Items);

var x = new Order(["A", "B"]);
var y = new Order(["A", "B"]); // different List instance

Console.WriteLine(x == y); // False — different List references
```

To compare content, override `Equals` and `GetHashCode`, or use `ImmutableArray<T>` / `IReadOnlyList<T>` with a custom comparer.

---

## Nondestructive mutation — `with` expressions

`with` creates a shallow copy of the record with one or more properties modified. The original is untouched.

```csharp
public record Person(string FirstName, string LastName, int Age);

var alice  = new Person("Alice", "Smith", 30);
var older  = alice with { Age = 31 };
var renamed = alice with { FirstName = "Alicia", LastName = "Jones" };

Console.WriteLine(alice);   // Person { FirstName = Alice, LastName = Smith, Age = 30 }
Console.WriteLine(older);   // Person { FirstName = Alice, LastName = Smith, Age = 31 }
Console.WriteLine(renamed); // Person { FirstName = Alicia, LastName = Jones, Age = 30 }
```

`with` works on any property that has an `init` or `set` accessor. Positional properties satisfy this automatically.

### Computed properties and `with`

Compute derived values from the property accessors (expression-body), not in the constructor. Otherwise `with` copies the cached (stale) value.

```csharp
// ✅ Correct — computed on access
public record Circle(double Radius)
{
    public double Area => Math.PI * Radius * Radius;
}

// ❌ Wrong — cached at construction, stale after with
public record CircleBad(double Radius)
{
    public double Area { get; } = Math.PI * Radius * Radius;
}

var c = new Circle(5);
var larger = c with { Radius = 10 };
Console.WriteLine(larger.Area); // ✅ 314.15... (correct)

var bad = new CircleBad(5);
var largerBad = bad with { Radius = 10 };
Console.WriteLine(largerBad.Area); // ❌ 78.53... (stale — still Radius 5's area)
```

---

## Built-in `ToString`

Records produce a structured string showing all public properties.

```csharp
public record Person(string FirstName, string LastName, int Age);
var p = new Person("Bob", "Lane", 42);
Console.WriteLine(p);
// Output: Person { FirstName = Bob, LastName = Lane, Age = 42 }
```

This makes records convenient for structured logging. You can customise by overriding `PrintMembers`:

```csharp
public record Person(string FirstName, string LastName)
{
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Name = {FirstName} {LastName}");
        return true;
    }
}
```

---

## Deconstruction

Positional records get a `Deconstruct` method automatically. The parameters mirror the primary constructor in order.

```csharp
public record Point(double X, double Y);

var p = new Point(3, 4);
var (x, y) = p;               // positional deconstruct
Console.WriteLine($"{x}, {y}"); // 3, 4

// Works in switch expressions
string Describe(Point pt) => pt switch
{
    (0, 0) => "origin",
    (var x2, 0) => $"on x-axis at {x2}",
    _ => $"({pt.X}, {pt.Y})"
};
```

Only properties declared via the primary constructor (positional) are included in `Deconstruct`. Explicitly declared additional properties are not.
