# bUnit Reference: Installation and JSInterop

## Installation

To install the bUnit test project template:

```bash
dotnet new install bunit.template
```

## JSInterop Setup

- Use `JSInterop.SetupVoid("functionName")` to set up a void JS interop call.
- Use `JSInterop.Setup<TResult>("functionName")` to set up a call returning a value.
- Use `.SetResult(value)` to specify the return value.
- Use `.SetVoidResult()` to mark a void invocation as completed.

See the official docs for more: https://bunit.dev/docs/test-doubles/emulating-ijsruntime
