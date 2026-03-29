# bUnit Reference: Mocking and Service Providers

## Custom Service Providers

- You can implement custom `IServiceProvider`, `IServiceScopeFactory`, and `IServiceProviderFactory` for advanced DI scenarios in bUnit tests.
- See the official docs for a full example: https://bunit.dev/docs/providing-input/inject-services-into-components

## Mocking Components

- Use NSubstitute or Moq to mock child components.
- Register mocks with `ComponentFactories.Add<T>(mock)`.
- Example (NSubstitute):
  ```csharp
  var barMock = Substitute.For<Bar>();
  ComponentFactories.Add<Bar>(barMock);
  ```
- Example (Moq):
  ```csharp
  var barMock = new Mock<Bar>();
  ComponentFactories.Add<Bar>(barMock.Object);
  ```

## Mocking HttpClient

- Use `Services.AddMockHttpClient()` to register a mock HttpClient.
- Configure responses with `.When(url).RespondJson(data)`.

See the official docs for more: https://bunit.dev/docs/test-doubles/mocking-httpclient
