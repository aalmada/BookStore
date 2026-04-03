# Record Inheritance

Inheritance only applies to `record class` types. `record struct` types cannot participate in inheritance hierarchies.

## Basic inheritance

A record can inherit from another record. A record cannot inherit from a plain class, and a plain class cannot inherit from a record.

```csharp
public abstract record Person(string FirstName, string LastName);

public record Student(string FirstName, string LastName, int Grade)
    : Person(FirstName, LastName);

public record Teacher(string FirstName, string LastName, string Subject)
    : Person(FirstName, LastName);
```

The derived record must forward the base record's primary constructor parameters in its base call. The base record owns and synthesises the corresponding properties; the derived record does not re-declare them.

---

## `abstract` records

Mark a record `abstract` to prevent direct instantiation while sharing common positional properties and synthesised members across derived types.

```csharp
public abstract record Shape(string Color);

public record Circle(string Color, double Radius)
    : Shape(Color);

public record Rectangle(string Color, double Width, double Height)
    : Shape(Color);
```

`Shape` cannot be instantiated directly, but `Circle` and `Rectangle` inherit its `Color` property, value-equality logic, and `ToString` formatting.

---

## Value equality in inheritance hierarchies

For two record variables to be equal, their **runtime types** must match, even if the declared variable type is a base record.

```csharp
Person teacher = new Teacher("Nancy", "Davolio", "Math");
Person student = new Student("Nancy", "Davolio", 10);

Console.WriteLine(teacher == student); // False — different runtime types

Person teacher2 = new Teacher("Nancy", "Davolio", "Math");
Console.WriteLine(teacher == teacher2); // True — same type and same data
```

The compiler uses an `EqualityContract` property (a `Type` value) that is checked before comparing data fields. Derived records automatically override `EqualityContract` to return their own type, so a `Teacher` can never equal a `Student` even if all declared-property values are the same.

---

## `with` on derived records

`with` always produces an instance of the runtime type (the derived type), not the declared type.

```csharp
Person t = new Teacher("Nancy", "Davolio", "Math");
Person t2 = t with { FirstName = "Alice" };

Console.WriteLine(t2.GetType().Name); // Teacher — runtime type is preserved
Console.WriteLine(t2); // Teacher { FirstName = Alice, LastName = Davolio, Subject = Math }
```

---

## `sealed` records

Seal a derived record to prevent further subclassing. This also lets the compiler generate a more efficient `Equals` implementation (no virtual dispatch needed).

```csharp
public abstract record Animal(string Name);

public sealed record Dog(string Name, string Breed) : Animal(Name);
```

---

## `PrintMembers` in derived records

For custom `ToString` behaviour, override `PrintMembers`. The base record's synthesised `ToString` calls `PrintMembers` virtually, so the derived override is picked up automatically.

```csharp
public abstract record Shape(string Color)
{
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Color = {Color}");
        return true;
    }
}

public record Circle(string Color, double Radius) : Shape(Color)
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append($", Radius = {Radius}");
        return true;
    }
}
```
