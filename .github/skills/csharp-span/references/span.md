# Span\<T\> and ReadOnlySpan\<T\>

`Span<T>` is a `ref struct` that points to a contiguous sequence of elements. It never owns the memory — it's always a view over something else. Creating a span (via `AsSpan()`, `Slice()`, `stackalloc`, or constructor) never copies the underlying data.

## Creating spans

```csharp
// From an array
int[] array = { 1, 2, 3, 4, 5 };
Span<int> full = array.AsSpan();              // whole array
Span<int> slice = array.AsSpan(1, 3);         // [2, 3, 4]

// Slicing an existing span
Span<int> head = full[..3];                   // first 3 elements
Span<int> tail = full[2..];                   // from index 2 onward
Span<int> mid  = full.Slice(1, 3);            // [2, 3, 4] — explicit form

// Stack allocation — no heap, no GC
Span<byte> buf = stackalloc byte[128];        // fine for small fixed sizes
Span<char> chars = stackalloc char[64];

// From a fixed-size inline array (C# 12 InlineArray)
// From unmanaged pointer (unsafe context)
unsafe { Span<byte> managed = new Span<byte>(ptr, length); }
```

> **Stackalloc safety**: only use for small, bounded sizes known at compile time. Large `stackalloc` can cause a stack overflow. For dynamic sizes, use `ArrayPool<byte>.Shared.Rent()` instead (see [memory.md](memory.md)).

## ReadOnlySpan\<T\>

Use `ReadOnlySpan<T>` whenever you don't need to modify the data. It accepts `string` directly (no allocation), enables broader API callability, and prevents accidental mutation.

```csharp
// From a string — zero allocation
ReadOnlySpan<char> text = "Hello, World!".AsSpan();
ReadOnlySpan<char> hello = text[..5];          // "Hello"

// Implicit conversion from Span<T>
Span<int> mutable = stackalloc int[4];
ReadOnlySpan<int> readOnly = mutable;          // widening, always safe

// Literal string as span (UTF-16)
static ReadOnlySpan<byte> Separator => ";\n"u8; // UTF-8 literal, .NET 7+
```

## Common methods

```csharp
Span<int> span = ...;

// Reading
int val = span[i];
int len = span.Length;
bool empty = span.IsEmpty;

// Searching
int idx = span.IndexOf(42);
int last = span.LastIndexOf(42);
bool contains = span.Contains(42);
int pos = span.IndexOfAny(stackalloc int[] { 1, 2, 3 });

// Modifying
span[i] = value;
span.Fill(0);
span.Clear();                                  // equivalent to Fill(default)
span.Reverse();

// Copying
source.CopyTo(destination);                    // throws if dest too small
bool ok = source.TryCopyTo(destination);       // returns false if too small

// Sorting
span.Sort();
span.Sort(comparer);

// Sequences
bool equal = a.SequenceEqual(b);
int cmp = a.SequenceCompareTo(b);
```

## Zero-allocation string operations

These methods avoid string allocations entirely by working on `ReadOnlySpan<char>`:

```csharp
ReadOnlySpan<char> input = "  hello world  ".AsSpan();

// Trimming
ReadOnlySpan<char> trimmed = input.Trim();
ReadOnlySpan<char> left    = input.TrimStart();
ReadOnlySpan<char> right   = input.TrimEnd();

// Searching
bool starts = input.StartsWith("  hello", StringComparison.Ordinal);
bool ends   = input.EndsWith("  ", StringComparison.Ordinal);
int  comma  = input.IndexOf(',');

// Comparison without allocation
bool eq = input.Equals("  hello world  ", StringComparison.OrdinalIgnoreCase);

// Splitting without allocation (.NET 8+)
foreach (Range range in "a,b,c".AsSpan().Split(','))
{
    ReadOnlySpan<char> part = "a,b,c".AsSpan()[range];
}
```

## Zero-allocation parsing

Built-in numeric types support `TryParse` and `TryFormat` directly on spans:

```csharp
ReadOnlySpan<char> text = "12345".AsSpan();

// Parsing — no string allocation
int.TryParse(text, out int intValue);
double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dbl);
Guid.TryParse(text, out Guid id);
DateTimeOffset.TryParse(text, out DateTimeOffset dto);

// Formatting into a buffer — no string allocation
Span<char> output = stackalloc char[32];
int written;
intValue.TryFormat(output, out written);
dbl.TryFormat(output, out written, "F2", CultureInfo.InvariantCulture);
dto.TryFormat(output, out written, "O");
```

## Inline string building (ISpanFormattable / TryFormat)

For building composite strings without `string.Format` or interpolation allocations:

```csharp
static bool TryBuildLabel(int id, double score, Span<char> buffer, out int written)
{
    written = 0;
    if (!id.TryFormat(buffer, out int n)) return false;
    written += n;
    buffer[written++] = ':';
    if (!score.TryFormat(buffer[written..], out n, "F2")) return false;
    written += n;
    return true;
}
```

Or use `MemoryExtensions.TryWrite` (a `string.Create`-like API for spans, .NET 6+):

```csharp
Span<char> buf = stackalloc char[64];
MemoryExtensions.TryWrite(buf, $"id={id}, score={score:F2}", out int written);
string result = new string(buf[..written]);
```

## Span\<byte\> and binary data

```csharp
Span<byte> bytes = stackalloc byte[8];

// Write a value into bytes
BitConverter.TryWriteBytes(bytes, 123456789);

// Read back
int value = BitConverter.ToInt32(bytes);

// Big-endian / little-endian (System.Buffers.Binary)
BinaryPrimitives.WriteInt32BigEndian(bytes, value);
int read = BinaryPrimitives.ReadInt32BigEndian(bytes);
```

## Returning spans from methods

`Span<T>` can only be returned if the underlying memory outlives the method. Returning stack memory is a compile-time error:

```csharp
// ❌ Compile error — stack memory escapes
Span<int> Bad() { return stackalloc int[4]; }

// ✅ Fine — array lives on the heap
Span<int> Good(int[] array) => array.AsSpan();

// ✅ Fine — slice of a parameter
Span<int> Tail(Span<int> span) => span[1..];
```

If you need to return a buffer that doesn't outlive the caller, use `Memory<T>` — see [memory.md](memory.md).
