# Generic Math Interface Catalogue

All interfaces live in `System.Numerics` unless noted. All built-in .NET numeric types implement the appropriate interfaces.

## Numeric interfaces (high-level)

| Interface | Description | Implemented by |
|-----------|-------------|----------------|
| `INumberBase<TSelf>` | Base for all numbers (real + complex). Defines `Zero`, `One`, `Create*`, `Is*` predicates. | All numeric types |
| `INumber<TSelf>` | Comparable real numbers. Adds `Clamp`, `Sign`, `Max`, `Min`, `CopySign`. | `int`, `float`, `double`, `decimal`, `Half`, … |
| `ISignedNumber<TSelf>` | Types that have a `NegativeOne` constant. | `int`, `float`, `double`, … |
| `IUnsignedNumber<TSelf>` | Marker for unsigned integers. | `byte`, `uint`, `ulong`, … |
| `IBinaryNumber<TSelf>` | Binary representation. Adds `IsPow2`, `Log2`, `AllBitsSet`. | All integer + IEEE float types |
| `IBinaryInteger<TSelf>` | Integer-specific ops: `DivRem`, `LeadingZeroCount`, `PopCount`, `RotateLeft/Right`, `TrailingZeroCount`. | `int`, `long`, `byte`, `short`, … |
| `IFloatingPoint<TSelf>` | Floating-point: `Ceiling`, `Floor`, `Round`, `Truncate`. | `float`, `double`, `Half`, `decimal` |
| `IFloatingPointIeee754<TSelf>` | IEEE 754 types with `NaN`, `Infinity`, `Epsilon`, `Pi`, `E`, `Tau`. | `float`, `double`, `Half` |
| `IBinaryFloatingPointIeee754<TSelf>` | Binary IEEE 754 (not `decimal`). | `float`, `double`, `Half` |
| `IAdditiveIdentity<TSelf,TResult>` | `T.AdditiveIdentity` (zero for +). | All numeric types |
| `IMultiplicativeIdentity<TSelf,TResult>` | `T.MultiplicativeIdentity` (one for *). | All numeric types |
| `IMinMaxValue<TSelf>` | `T.MinValue` and `T.MaxValue`. | Most numeric types |

## Operator interfaces

Each corresponds to one or more C# operators. All three type parameters can differ to support mixed-type arithmetic (e.g., `int / int → double`).

| Interface | Operators |
|-----------|-----------|
| `IAdditionOperators<TSelf,TOther,TResult>` | `x + y` |
| `ISubtractionOperators<TSelf,TOther,TResult>` | `x - y` |
| `IMultiplyOperators<TSelf,TOther,TResult>` | `x * y` |
| `IDivisionOperators<TSelf,TOther,TResult>` | `x / y` |
| `IModulusOperators<TSelf,TOther,TResult>` | `x % y` |
| `IUnaryNegationOperators<TSelf,TResult>` | `-x` |
| `IUnaryPlusOperators<TSelf,TResult>` | `+x` |
| `IIncrementOperators<TSelf>` | `++x`, `x++` |
| `IDecrementOperators<TSelf>` | `--x`, `x--` |
| `IBitwiseOperators<TSelf,TOther,TResult>` | `& \| ^ ~` |
| `IShiftOperators<TSelf,TOther,TResult>` | `<< >>` |
| `IComparisonOperators<TSelf,TOther,TResult>` | `< > <= >=` |
| `IEqualityOperators<TSelf,TOther,TResult>` | `== !=` |

> **Checked operators**: Some interfaces define a *checked* variant (e.g., `CheckedSubtraction`) for overflow detection in `checked {}` contexts. If you implement the checked variant, you must also implement the unchecked one.

## Function interfaces (floating-point)

All implemented by `IFloatingPointIeee754<TSelf>` (`float`, `double`, `Half`).

| Interface | Key methods |
|-----------|-------------|
| `IExponentialFunctions<TSelf>` | `Exp`, `Exp2`, `Exp10`, `ExpM1`, `Exp2M1`, `Exp10M1` |
| `ILogarithmicFunctions<TSelf>` | `Log`, `Log2`, `Log10`, `LogP1`, `Log2P1`, `Log10P1` |
| `IPowerFunctions<TSelf>` | `Pow` |
| `IRootFunctions<TSelf>` | `Sqrt`, `Cbrt`, `Hypot`, `RootN` |
| `ITrigonometricFunctions<TSelf>` | `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `SinCos` |
| `IHyperbolicFunctions<TSelf>` | `Sinh`, `Cosh`, `Tanh`, `Asinh`, `Acosh`, `Atanh` |

Constants (via `IFloatingPointConstants<TSelf>`): `Pi`, `E`, `Tau`.

## Parsing & formatting interfaces (System namespace)

| Interface | What it enables |
|-----------|-----------------|
| `IParsable<TSelf>` | `T.Parse(string, IFormatProvider)` / `T.TryParse(...)` |
| `ISpanParsable<TSelf>` | `T.Parse(ReadOnlySpan<char>, IFormatProvider)` |
| `IFormattable` | `value.ToString(format, provider)` |
| `ISpanFormattable` | `value.TryFormat(Span<char>, ...)` |

## What the built-in types implement

| Type | `INumber<T>` | `IFloatingPointIeee754<T>` | `IBinaryInteger<T>` |
|------|:---:|:---:|:---:|
| `byte`, `sbyte`, `short`, `ushort` | ✅ | ❌ | ✅ |
| `int`, `uint`, `long`, `ulong` | ✅ | ❌ | ✅ |
| `Int128`, `UInt128`, `nint`, `nuint` | ✅ | ❌ | ✅ |
| `float` (`Single`) | ✅ | ✅ | ❌ |
| `double` (`Double`) | ✅ | ✅ | ❌ |
| `Half` | ✅ | ✅ | ❌ |
| `decimal` | ✅ | ❌ | ❌ |
| `char`, `Guid`, `DateTime`, `TimeSpan` | partial | ❌ | ❌ |
