# bUnit Reference: Test Patterns and Async

## Basic Test Patterns (All Frameworks)

- bUnit supports TUnit, xUnit, NUnit, and MSTest.
- Inherit from the appropriate test context:
	- TUnit: `BunitTestContext` (with `[Test] async Task` and TUnit's fluent assertions)
	- xUnit: `BunitContext` (with `[Fact]`)
	- NUnit: `BunitContext` (with `[Test]`)
	- MSTest: `BunitContext` (with `[TestMethod]`)
- Use `RenderComponent<TComponent>()` or `Render(@<Component/>)` to render components.
- Use `MarkupMatches` to compare rendered output semantically.
- For TUnit, prefer: `await Assert.That(actual).IsEqualTo(expected);`

## Async Component Testing

- Simulate async loading in components with `await Task.Delay(...)` in `OnInitializedAsync`.
- Test async state transitions by rendering, then awaiting state changes.

## JS Module Interop

- Use `JSInterop.SetupModule("module.js")` to mock JS module imports.
- Set up function handlers on the module mock as needed.

See the official docs for more: https://bunit.dev/docs/interaction/awaiting-async-state
