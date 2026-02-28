# Refactor Request Router

## Spec
`specs/server`

## Problem

`DynamoDbRequestRouter` is the single entry point for all HTTP requests. It currently handles five distinct responsibilities in one 114-line class:

1. **Health check** — Handles `GET /` with a hardcoded JSON response (lines 19–24)
2. **HTTP validation** — Validates request method, path, and `X-Amz-Target` header format (lines 26–46)
3. **Operation dispatch** — Maps operation names to typed handler methods via a switch expression (lines 52–69)
4. **JSON serialization** — Deserializes request bodies and serializes responses in `Dispatch<TReq, TRes>` (lines 87–92)
5. **Error formatting** — Converts `DynamoDbException` and `JsonException` into DynamoDB-format error responses, including special-case `TransactionCanceledException` with `CancellationReasons` (lines 75–84, 94–113)

This violates the Single Responsibility Principle. The health check is unrelated to DynamoDB dispatch. Error formatting is a cross-cutting concern that doesn't belong in the router. As the server grows (more operations, richer error handling, observability), this class becomes the dumping ground for unrelated changes.

The constructor already takes 5 operation class dependencies — adding any new operation group means modifying both the constructor signature and the switch body.

## Solution

Decompose `DynamoDbRequestRouter` into focused classes using ASP.NET Core's middleware pipeline and minimal API endpoints:

- **Health check** → `MapHealthChecks` in `Program.cs`
- **Error formatting** → Middleware that wraps downstream handlers in try/catch
- **HTTP validation** → Middleware that validates method, path, and headers before dispatch
- **Operation dispatch** → Router does a dictionary lookup against `IDynamoDbCommand` implementations
- **Serialization + execution** → Each operation is a typed `DynamoDbCommand<TRequest, TResponse>` that handles its own deserialization, execution, and serialization

Each command class has one reason to change. The router has zero knowledge of specific operations.

## Scope

- Extract health check from `DynamoDbRequestRouter` to a `MapHealthChecks` call in `Program.cs`
- Extract DynamoDB error formatting to an error-handling middleware
- Extract HTTP/header validation to a validation middleware
- Introduce `IDynamoDbCommand` interface and `DynamoDbCommand<TRequest, TResponse>` abstract base class
- Implement one command class per operation (14 total), grouped into 5 files by operation group
- Replace the switch expression and `Dispatch<TReq,TRes>` in the router with a dictionary lookup against `IDynamoDbCommand`
- Update `Program.cs` to wire the middleware pipeline and register commands against `IDynamoDbCommand`
- Update `specs/server/spec.md` to document the middleware architecture

## Out of Scope

- Changing the operation classes (`TableOperations`, `ItemOperations`, etc.) — they are not affected
- Adding MVC controllers — DynamoDB routes all operations to `POST /` via header, which doesn't map to controller-based URL routing
- Adding new DynamoDB operations
- Changing error types, status codes, or error response format
- Changing JSON serialization options or `AttributeValueConverter`

## Design Feedback

The following changes were made to the initial proposal following review by Chris:

**Health check: drop the `HealthCheckHandler` class**
The initial draft proposed a `HealthCheckHandler` static class with a `MapHealthCheck` extension method. Chris pointed out this is a one-liner and doesn't warrant a separate class — it should live directly in `Program.cs`. Accepted: health check is now a `MapHealthChecks` call inline in `Program.cs`.

**Health check: use the built-in ASP.NET Core framework**
Chris noted that ASP.NET Core has built-in health check extension methods (`AddHealthChecks()` / `MapHealthChecks()`). The initial draft used a raw `MapGet`. Accepted: switched to `builder.Services.AddHealthChecks()` + `app.MapHealthChecks()` with a custom `ResponseWriter`, which is more extensible and idiomatic.

**Health check: add `/healthz`**
Chris requested the health check also respond at the standard `/healthz` path, in addition to `/`. Accepted: both paths are mapped with a shared `HealthCheckOptions` instance.

**`HttpContext.Items` for operation name: remove it**
The initial design stored the parsed operation name in `context.Items["DynamoDb.Operation"]` in the validation middleware and read it back in the router. Chris flagged this as unpleasant (a stringly-typed dictionary lookup with a cast). Accepted: the router reads `X-Amz-Target` directly — the validation middleware has already guaranteed it is valid, so re-parsing is safe and clearer.

**Command pattern instead of `Dispatch<TReq, TRes>`**
The initial design retained a generic `Dispatch<TReq, TRes>(Stream body, Func<TReq, TRes> handler)` method inside the router, with a 14-arm switch expression passing operation methods as `Func` delegates. Chris observed this was an unnecessarily dynamic/functional style for C# — each operation should be a proper typed class. Accepted: replaced with an `IDynamoDbCommand` interface, a `DynamoDbCommand<TRequest, TResponse>` abstract base class handling serialization, and 14 concrete command classes. The router becomes a dictionary dispatcher with no knowledge of specific operations.
