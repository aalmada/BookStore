# Common Record Mistakes and Pitfalls

## 1. Using records as EF Core entity types

Entity Framework Core depends on reference equality to track entities. Records use value equality by default, which breaks change tracking.

```csharp
// ❌ Do not map records directly as EF Core entities
public record Product(Guid Id, string Name, decimal Price);

// ✅ Use a class instead
public class Product
{
    public Guid    Id    { get; set; }
    public string  Name  { get; set; } = default!;
    public decimal Price { get; set; }
}
```

If you want an immutable view of an EF entity, project into a record in your queries rather than making the entity itself a record.

---

## 2. Caching computed properties — stale values after `with`

Properties computed and cached at construction time hold the original value even after a `with` expression changes a dependency.

```csharp
// ❌ Cached at construction — wrong after with
public record Circle(double Radius)
{
    public double Area { get; } = Math.PI * Radius * Radius;
}

var c = new Circle(5);
var d = c with { Radius = 10 };
Console.WriteLine(d.Area); // ❌ returns area of radius 5

// ✅ Computed on demand — always correct
public record Circle(double Radius)
{
    public double Area => Math.PI * Radius * Radius;
}
```

If the computation is expensive and you need caching, use a `class` where you can recompute the cached value when a setter fires.

---

## 3. Assuming deep immutability

The `init` accessor guards the property reference, not the referenced object's content.

```csharp
public record Order(List<string> Items);

var order = new Order(["A"]);
order.Items.Add("B"); // Compiles and runs — list is mutable
```

Use `ImmutableArray<T>` or `IReadOnlyList<T>` with a defensive copy when you need deep immutability.

---

## 4. Expecting `Deconstruct` for non-positional properties

`Deconstruct` is only synthesised for positional parameters. Additional properties declared in the body are not included.

```csharp
public record Person(string FirstName, string LastName)
{
    public int Age { get; init; }
}

var (first, last) = new Person("A", "B") { Age = 30 };
// Age is NOT part of the deconstruct — no way to get it via tuple syntax
```

If you need `Age` in a deconstruction, either make it a positional parameter or write a custom `Deconstruct` overload.

---

## 5. Omitting base constructor call in derived records

Every positional parameter defined in a base record must be forwarded explicitly in the derived record's base call, or the compiler will error.

```csharp
// ❌ Missing base constructor forward
public record Student(string FirstName, string LastName, int Grade)
    : Person(); // error — Person requires FirstName and LastName

// ✅ Correct
public record Student(string FirstName, string LastName, int Grade)
    : Person(FirstName, LastName);
```

---

## 6. Mixing record and class inheritance

Records cannot inherit from plain classes (except `object`), and classes cannot inherit from records.

```csharp
// ❌
public class Entity { public Guid Id { get; set; } }
public record Product(string Name) : Entity; // compile error

// ✅ Option A: make Entity a record too
public abstract record Entity(Guid Id);
public record Product(Guid Id, string Name) : Entity(Id);

// ✅ Option B: include Id as a positional parameter
public record Product(Guid Id, string Name);
```

---

## 7. Using `record struct` when you meant `readonly record struct`

`record struct` positional properties are **mutable** (read-write), which is rarely the intended semantics.

```csharp
// ❌ Probably wrong — mutable value type
public record struct Point(double X, double Y);

// ✅ Immutable value type
public readonly record struct Point(double X, double Y);
```

---

## 8. Overriding `Equals`/`GetHashCode` without keeping them in sync

Records generate both together. If you override one, override the other to maintain the contract: equal objects must have equal hash codes.

```csharp
public record TaggedPoint(double X, double Y, string Tag)
{
    // Override equality to ignore Tag
    public virtual bool Equals(TaggedPoint? other)
        => other is not null && X == other.X && Y == other.Y;

    public override int GetHashCode() => HashCode.Combine(X, Y); // ✅ consistent
}
```

---

## When to use a class instead of a record

| Scenario | Prefer |
|----------|--------|
| EF Core / ORM entity | `class` |
| Objects with identity (not value) | `class` |
| Mutable complex state with business rules | `class` |
| Heavy cached-computed properties that change | `class` |
| Thread-safe, data-only, value semantics | `record` |
| Tiny allocation-sensitive value type | `readonly record struct` |
