# Immutability in C# Records

## What "immutable" means for records

Positional properties in `record class` and `readonly record struct` use `init`-only accessors. After the object is constructed, you cannot reassign those properties — but this is **shallow immutability**.

- You cannot change *which* object a reference property points to.
- You *can* change the *content* of the referenced object (if it is mutable itself).

```csharp
public record Order(Guid Id, List<string> Items);

var order = new Order(Guid.CreateVersion7(), ["Book A"]);
// order.Items = new List<string>(); // ❌ compile error — reference is init-only
order.Items.Add("Book B");           // ✅ list content is mutable
```

If you need deep immutability, use immutable collection types (`ImmutableArray<T>`, `ImmutableList<T>`) or arrays that you do not expose for mutation.

---

## `init` accessor

Properties declared with `init` can only be set during object initialisation (constructor call or object-initialiser block). This is how the compiler makes positional record properties immutable.

```csharp
public record Product
{
    public required Guid   Id    { get; init; }
    public required string Name  { get; init; }
    public decimal         Price { get; init; }
}

var p = new Product { Id = Guid.CreateVersion7(), Name = "Notebook", Price = 9.99m };
// p.Price = 11m; // ❌ compile error
var updated = p with { Price = 11m }; // ✅ create a new copy
```

---

## Primary constructors and immutability

When you declare a `record` with a primary constructor, each parameter becomes an `init`-only auto-property automatically.

```csharp
// Compiler synthesises:
//   public string FirstName { get; init; }
//   public string LastName  { get; init; }
public record Person(string FirstName, string LastName);
```

You can override the synthesised property to change accessibility, add a custom getter, or provide a field-backed implementation — but you must still initialise it from the parameter.

```csharp
public record Person(string FirstName, string LastName)
{
    // Custom validation in getter body, still init-only
    public string FirstName { get; init; } =
        string.IsNullOrWhiteSpace(FirstName)
            ? throw new ArgumentException("Cannot be blank", nameof(FirstName))
            : FirstName;
}
```

---

## Making a positional property mutable

Override with a `set` accessor. This is unusual — prefer `with` expressions instead.

```csharp
public record Config(string ConnectionString)
{
    public string ConnectionString { get; set; } = ConnectionString; // mutable
}
```

---

## `readonly record struct` vs `record struct`

| | `readonly record struct` | `record struct` |
|--|--|--|
| Positional properties | `init`-only (immutable) | read-write (mutable) |
| Can be used as `in` param without copy | ✅ | ❌ |
| Value equality | ✅ | ✅ |

---

## When immutability is NOT appropriate

- **ORM entity types** (e.g., Entity Framework Core) — they rely on reference equality and need mutable properties.
- **Objects with expensive computed state** that must be cached — if the cache depends on a property being stable, mutability through `with` (which creates a copy) may surprise callers; use a regular `class` instead.
- **Large value types on the stack** — if the struct has many fields, copies created by `with` or assignment may be costly; benchmark before committing.

---

## Benefits of immutability

- **Thread safety** — immutable records can be shared across threads without locking.
- **Safe dictionary keys / hash-set members** — `GetHashCode` based on data won't shift after insertion.
- **Predictable `with` expressions** — creating a modified copy never affects the original.
- **Easier reasoning** — you always know a record's data is exactly what was set at construction.
