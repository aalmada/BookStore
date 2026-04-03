# C# Record Declaration Forms

C# offers four flavours of record. Pick the one that matches the semantics you need.

## 1. `record` / `record class` — reference type, immutable positional properties (C# 9+)

The most common form. `record` and `record class` are synonyms; adding `class` is optional but improves readability.

### Positional syntax (primary constructor)

The compiler synthesises `init`-only properties, a primary constructor, and a `Deconstruct` method.

```csharp
public record Person(string FirstName, string LastName);
// Usage:
var p = new Person("Jane", "Doe");
var (first, last) = p; // Deconstruct works automatically
```

### Explicit-property syntax

Use when you need control over accessibility, nullability annotations, or want `required` enforcement.

```csharp
public record Person
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}
```

### Mixing positional and explicit members

Positional parameters generate public `init` properties by default. You can override individual properties to change accessibility, add validation, or back the value with a field.

```csharp
public record Person(string FirstName, string LastName, string Id)
{
    // Override to restrict visibility
    internal string Id { get; init; } = Id;
}
```

Back with a readonly field instead of a property:
```csharp
public record Person(string FirstName, string LastName, string Id)
{
    internal readonly string Id = Id;
}
```

### Applying attributes to positional properties

Use `property:`, `field:`, or `param:` targets on attributes applied to positional parameters.

```csharp
public record Product(
    [property: JsonPropertyName("product_id")] Guid ProductId,
    [property: JsonPropertyName("name")]       string Name);
```

---

## 2. `readonly record struct` — value type, immutable (C# 10+)

Generates the same synthesised members as `record class`, but as a value type. Best for small (2–4 fields) frequently-allocated types where you want to avoid heap allocations.

```csharp
public readonly record struct Point(double X, double Y);
// Usage:
var origin = new Point(0, 0);
var moved  = origin with { X = 3 }; // returns new Point(3, 0)
```

Positional properties are `init`-only (immutable after construction), matching `record class` semantics.

---

## 3. `record struct` — value type, mutable (C# 10+)

Positional properties are **read-write** — the struct is mutable by default. Rarely the right choice; prefer `readonly record struct` unless mutability is a strict requirement.

```csharp
public record struct Measurement(DateTime TakenAt, double Value);
// TakenAt and Value have public get/set accessors
```

---

## 4. Records without positional parameters

A record can declare zero positional parameters and use only normal property syntax. You still get value equality and `with`, but no synthesised constructor or `Deconstruct`.

```csharp
public record Address
{
    public required string Street { get; init; }
    public required string City   { get; init; }
    public required string Country { get; init; }
}
```

---

## Version availability quick reference

| Feature | C# version | .NET version |
|---------|-----------|-------------|
| `record` / `record class` | 9 | .NET 5+ |
| `record struct` / `readonly record struct` | 10 | .NET 6+ |
| Primary constructors on non-record types | 12 | .NET 8+ |

---

## Choosing between forms

```
Need reference semantics?
  Yes → record class (default: record)
  No  → value type variant

Need immutability?
  Yes → record class   OR   readonly record struct
  No  → record struct  (unusual — revisit the design)

Hot allocation path where heap pressure matters?
  Yes → readonly record struct
  No  → record class
```
