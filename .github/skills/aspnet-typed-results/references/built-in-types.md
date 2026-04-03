# Built-in TypedResults Reference

All methods live on `Microsoft.AspNetCore.Http.TypedResults`. The concrete return types live in `Microsoft.AspNetCore.Http.HttpResults`.

## Success responses

| Method | HTTP | Return type | Notes |
|--------|------|-------------|-------|
| `TypedResults.Ok()` | 200 | `Ok` | No body |
| `TypedResults.Ok(value)` | 200 | `Ok<T>` | JSON body |
| `TypedResults.Created(uri, value)` | 201 | `Created<T>` | Location header + body |
| `TypedResults.CreatedAtRoute(name, routeValues, value)` | 201 | `CreatedAtRoute<T>` | Named route |
| `TypedResults.Accepted(uri, value)` | 202 | `Accepted<T>` | Async accepted |
| `TypedResults.AcceptedAtRoute(name, routeValues, value)` | 202 | `AcceptedAtRoute<T>` | Named route |
| `TypedResults.NoContent()` | 204 | `NoContent` | No body |

## Client error responses

| Method | HTTP | Return type | Notes |
|--------|------|-------------|-------|
| `TypedResults.BadRequest()` | 400 | `BadRequest` | No body |
| `TypedResults.BadRequest(error)` | 400 | `BadRequest<T>` | Typed error body |
| `TypedResults.Unauthorized()` | 401 | `UnauthorizedHttpResult` | |
| `TypedResults.Forbid()` | 403 | `ForbidHttpResult` | |
| `TypedResults.NotFound()` | 404 | `NotFound` | No body |
| `TypedResults.NotFound(value)` | 404 | `NotFound<T>` | Typed body |
| `TypedResults.Conflict()` | 409 | `Conflict` | No body |
| `TypedResults.Conflict(error)` | 409 | `Conflict<T>` | Typed error body |
| `TypedResults.UnprocessableEntity()` | 422 | `UnprocessableEntity` | |
| `TypedResults.UnprocessableEntity(error)` | 422 | `UnprocessableEntity<T>` | |

## Problem details

| Method | HTTP | Return type | Notes |
|--------|------|-------------|-------|
| `TypedResults.Problem(detail, ...)` | varies | `ProblemHttpResult` | RFC 7807 ProblemDetails |
| `TypedResults.ValidationProblem(errors)` | 400 | `ValidationProblem` | Validation error map |
| `TypedResults.InternalServerError()` | 500 | `InternalServerError` | |
| `TypedResults.InternalServerError(error)` | 500 | `InternalServerError<T>` | |

## File and stream responses

| Method | Return type | Notes |
|--------|-------------|-------|
| `TypedResults.File(path, contentType)` | `PhysicalFileHttpResult` | Physical file on disk |
| `TypedResults.File(bytes, contentType)` | `FileContentHttpResult` | In-memory bytes |
| `TypedResults.Stream(stream, contentType)` | `PushStreamHttpResult` | Streaming |
| `TypedResults.Bytes(bytes, contentType)` | `FileContentHttpResult` | Alias for File(bytes) |
| `TypedResults.Text(text, contentType)` | `Utf8ContentHttpResult` | Plain text |
| `TypedResults.Content(content, contentType)` | `ContentHttpResult` | Arbitrary content |

## Redirect responses

| Method | Return type |
|--------|-------------|
| `TypedResults.Redirect(url)` | `RedirectHttpResult` |
| `TypedResults.LocalRedirect(url)` | `RedirectHttpResult` |
| `TypedResults.RedirectToRoute(name, routeValues)` | `RedirectToRouteHttpResult` |

## JSON serialisation control

| Method | Return type | Notes |
|--------|-------------|-------|
| `TypedResults.Json(value, options)` | `JsonHttpResult<T>` | Explicit serialiser options |

## Authentication / authorisation

| Method | Return type | Notes |
|--------|-------------|-------|
| `TypedResults.SignIn(principal, scheme)` | `SignInHttpResult` | |
| `TypedResults.SignOut(scheme)` | `SignOutHttpResult` | |
| `TypedResults.Challenge(scheme)` | `ChallengeHttpResult` | |

## Real-time

| Method | Return type | Notes |
|--------|-------------|-------|
| `TypedResults.ServerSentEvents(stream)` | `PushStreamHttpResult` | .NET 9+ SSE support |

## Arbitrary status

| Method | Return type | Notes |
|--------|-------------|-------|
| `TypedResults.StatusCode(code)` | `StatusCodeHttpResult` | Use sparingly; loses type info |

## HttpResult runtime interfaces

These interfaces let you inspect a result in filters or middleware without knowing the concrete type:

| Interface | What it exposes |
|-----------|----------------|
| `IStatusCodeHttpResult` | `.StatusCode` |
| `IValueHttpResult<T>` | `.Value` |
| `IContentTypeHttpResult` | `.ContentType` |
| `IFileHttpResult` | `.FileDownloadName`, `.ContentType` |
| `INestedHttpResult` | `.Result` (for union types) |

These are useful in test assertions when you want to check the status code without casting to the exact concrete type.
