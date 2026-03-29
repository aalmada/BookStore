# bUnit Reference: Localization, Auth, and Structure

## Localization

- Register localization services in your test setup:
  ```csharp
  BunitContext.Services.AddLocalization();
  ```

## Authentication & Authorization

- Use `AuthenticationStateProvider`, `CascadingAuthenticationState`, and `AuthorizeView` to test auth scenarios in Blazor components.
- Example usage in a component:
  ```razor
  <AuthorizeView>
    <Authorized>...</Authorized>
    <NotAuthorized>...</NotAuthorized>
  </AuthorizeView>
  ```

See the official docs for more: https://bunit.dev/docs/misc-test-tips
