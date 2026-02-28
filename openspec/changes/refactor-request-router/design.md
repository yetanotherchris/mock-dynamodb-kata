# Design

## Architecture

The current monolithic request handler is decomposed into an ASP.NET Core middleware pipeline:

```
Before:
  HTTP Request → DynamoDbRequestRouter.HandleRequest() → (does everything) → HTTP Response

After:
  HTTP Request
    → MapHealthChecks "/" and "/healthz" (short-circuits GET health check requests)
    → DynamoDbErrorMiddleware (wraps downstream in try/catch, formats error responses)
    → DynamoDbValidationMiddleware (validates POST /, parses X-Amz-Target, short-circuits invalid requests)
    → DynamoDbRequestRouter (dictionary lookup → IDynamoDbCommand.HandleAsync → write response)
       └─ DynamoDbCommand<TRequest, TResponse> (deserialize → Execute → serialize)
```

Each middleware either short-circuits (returns a response directly) or calls `next()` to pass control downstream. This follows ASP.NET Core's standard middleware pattern.

## Health Check

Uses ASP.NET Core's built-in health check framework (`Microsoft.Extensions.Diagnostics.HealthChecks`, included in the shared framework — no extra NuGet needed). Mapped to both `GET /` (backward compatibility) and `GET /healthz` (standard convention) with a custom response writer to match the existing JSON body.

```csharp
// Program.cs — DI registration
builder.Services.AddHealthChecks();

// Program.cs — endpoint mapping (shared options)
var healthCheckOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
    }
};
app.MapHealthChecks("/", healthCheckOptions);
app.MapHealthChecks("/healthz", healthCheckOptions);
```

This is extensible — custom `IHealthCheck` implementations can be added later (e.g., to report table count or memory usage) without changing the endpoint wiring.

## New Classes

### `Middleware/DynamoDbErrorMiddleware.cs`

Middleware that wraps all downstream handlers in a try/catch. Catches `DynamoDbException` and `JsonException` and formats them as DynamoDB-format error responses.

```csharp
namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DynamoDbException ex)
        {
            await WriteError(context, ex.StatusCode, ex.ErrorType, ex.Message, ex);
        }
        catch (JsonException)
        {
            await WriteError(context, 400,
                "com.amazonaws.dynamodb.v20120810#SerializationException",
                "Start of structure or map found where not expected");
        }
    }

    private static async Task WriteError(
        HttpContext context, int statusCode, string errorType, string message,
        DynamoDbException? ex = null)
    {
        context.Response.ContentType = "application/x-amz-json-1.0";
        context.Response.StatusCode = statusCode;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("__type", errorType);
            writer.WriteString("Message", message);
            if (ex is TransactionCanceledException txEx)
            {
                writer.WritePropertyName("CancellationReasons");
                JsonSerializer.Serialize(writer, txEx.CancellationReasons);
            }
            writer.WriteEndObject();
        }
        await context.Response.Body.WriteAsync(stream.ToArray());
    }
}
```

**Why middleware, not a static helper?** Error handling is a cross-cutting concern. As middleware, it automatically wraps all downstream handlers — the router and validation middleware don't need to know about error formatting. This also makes the error handling testable independently by constructing the middleware with a fake `RequestDelegate`.

**`TransactionCanceledException` special case:** The `CancellationReasons` array is specific to transaction errors and must appear in the error response. This special-casing stays in the error middleware because it's a serialization concern (how errors are formatted), not a business logic concern.

### `Middleware/DynamoDbValidationMiddleware.cs`

Middleware that validates incoming requests before they reach the router. Checks:
1. Request is `POST /` (returns 404 otherwise)
2. `X-Amz-Target` header is present (returns `MissingAuthenticationTokenException`)
3. `X-Amz-Target` has the correct `DynamoDB_20120810.` prefix (returns `UnknownOperationException`)

If valid, stores the parsed operation name in `HttpContext.Items` and calls `next()`.

```csharp
namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbValidationMiddleware(RequestDelegate next)
{
    private const string TargetPrefix = "DynamoDB_20120810.";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != "POST" || context.Request.Path != "/")
        {
            context.Response.StatusCode = 404;
            return;
        }

        var target = context.Request.Headers["X-Amz-Target"].FirstOrDefault();
        if (string.IsNullOrEmpty(target))
        {
            throw new DynamoDbException(
                "Missing Authentication Token",
                "com.amazonaws.dynamodb.v20120810#MissingAuthenticationTokenException");
        }

        if (!target.StartsWith(TargetPrefix))
        {
            throw new UnknownOperationException();
        }

        context.Items["DynamoDb.Operation"] = target[TargetPrefix.Length..];
        await next(context);
    }
}
```

**Why throw instead of writing errors directly?** The `DynamoDbErrorMiddleware` sits upstream and catches all `DynamoDbException` instances. By throwing, the validation middleware delegates error formatting to a single place. This eliminates the duplication where both the router and validation code independently format error responses.

**Why store in `HttpContext.Items`?** The validation middleware has already parsed the operation name — storing it avoids the router re-parsing the header. However, this is optional: the router can safely re-read and parse the header directly since the middleware has already validated it. The implementation may choose either approach.

**404 for non-POST requests:** The 404 response for wrong method/path is not a DynamoDB error — it's an HTTP-level concern. Writing the status code directly (without throwing) is correct because `DynamoDbErrorMiddleware` should only handle DynamoDB-specific errors.

## Command Pattern

Each DynamoDB operation is represented by a typed command class. A non-generic `IDynamoDbCommand` interface allows the router to hold a heterogeneous collection of commands. An abstract generic base class `DynamoDbCommand<TRequest, TResponse>` handles JSON deserialization and serialization, so concrete commands contain only their own logic.

### `Commands/IDynamoDbCommand.cs`

```csharp
namespace MockDynamoDB.Server.Commands;

public interface IDynamoDbCommand
{
    string OperationName { get; }
    Task<byte[]> HandleAsync(Stream body, JsonSerializerOptions options);
}

public abstract class DynamoDbCommand<TRequest, TResponse> : IDynamoDbCommand
{
    public abstract string OperationName { get; }

    public async Task<byte[]> HandleAsync(Stream body, JsonSerializerOptions options)
    {
        var request = await JsonSerializer.DeserializeAsync<TRequest>(body, options);
        var response = Execute(request!);
        return JsonSerializer.SerializeToUtf8Bytes(response, options);
    }

    protected abstract TResponse Execute(TRequest request);
}
```

### `Commands/TableCommands.cs`

Commands are grouped by operation class to match the existing code organisation:

```csharp
namespace MockDynamoDB.Server.Commands;

public sealed class CreateTableCommand(TableOperations ops) : DynamoDbCommand<CreateTableRequest, CreateTableResponse>
{
    public override string OperationName => "CreateTable";
    protected override CreateTableResponse Execute(CreateTableRequest request) => ops.CreateTable(request);
}

public sealed class DeleteTableCommand(TableOperations ops) : DynamoDbCommand<DeleteTableRequest, DeleteTableResponse>
{
    public override string OperationName => "DeleteTable";
    protected override DeleteTableResponse Execute(DeleteTableRequest request) => ops.DeleteTable(request);
}

public sealed class DescribeTableCommand(TableOperations ops) : DynamoDbCommand<DescribeTableRequest, DescribeTableResponse>
{
    public override string OperationName => "DescribeTable";
    protected override DescribeTableResponse Execute(DescribeTableRequest request) => ops.DescribeTable(request);
}

public sealed class ListTablesCommand(TableOperations ops) : DynamoDbCommand<ListTablesRequest, ListTablesResponse>
{
    public override string OperationName => "ListTables";
    protected override ListTablesResponse Execute(ListTablesRequest request) => ops.ListTables(request);
}
```

Similarly for **`ItemCommands.cs`** (PutItem, GetItem, DeleteItem, UpdateItem), **`QueryScanCommands.cs`** (Query, Scan), **`BatchCommands.cs`** (BatchGetItem, BatchWriteItem), and **`TransactionCommands.cs`** (TransactWriteItems, TransactGetItems).

## Changed Classes

### `Middleware/DynamoDbRequestRouter.cs`

The router becomes a generic dictionary dispatcher with no knowledge of specific operations. New commands are registered in DI without touching this class.

```csharp
namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbRequestRouter
{
    private const string TargetPrefix = "DynamoDB_20120810.";
    private static readonly JsonSerializerOptions JsonOptions = DynamoDbJsonOptions.Options;
    private readonly Dictionary<string, IDynamoDbCommand> _commands;

    public DynamoDbRequestRouter(IEnumerable<IDynamoDbCommand> commands)
    {
        _commands = commands.ToDictionary(c => c.OperationName);
    }

    public async Task HandleRequest(HttpContext context)
    {
        var operation = context.Request.Headers["X-Amz-Target"].FirstOrDefault()![TargetPrefix.Length..];

        if (!_commands.TryGetValue(operation, out var command))
            throw new UnknownOperationException();

        var result = await command.HandleAsync(context.Request.Body, JsonOptions);
        context.Response.ContentType = "application/x-amz-json-1.0";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(result);
    }
}
```

**What was removed:**
- Health check handling — now in `MapHealthChecks`
- HTTP method/path validation — now in `DynamoDbValidationMiddleware`
- `X-Amz-Target` validation (presence and prefix) — now in `DynamoDbValidationMiddleware`
- `WriteError` and catch blocks — now in `DynamoDbErrorMiddleware`
- The 14-arm switch expression and `Dispatch<TReq, TRes>` — now in individual command classes
- The 5 operation class constructor parameters

**What remains:**
- `X-Amz-Target` header parsing (one line — safe because validation middleware already checked it)
- Dictionary lookup and dispatch to the matched command
- Writing the 200 response

### `Program.cs`

Commands are registered as `IDynamoDbCommand` so the router receives them all via `IEnumerable<IDynamoDbCommand>`:

```csharp
using MockDynamoDB.Server.Commands;
using MockDynamoDB.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddSingleton<ITableStore, InMemoryTableStore>();
builder.Services.AddSingleton<IItemStore, InMemoryItemStore>();
builder.Services.AddSingleton<ReaderWriterLockSlim>();
builder.Services.AddSingleton<TableOperations>();
builder.Services.AddSingleton<ItemOperations>();
builder.Services.AddSingleton<QueryScanOperations>();
builder.Services.AddSingleton<BatchOperations>();
builder.Services.AddSingleton<TransactionOperations>();

// Commands registered against IDynamoDbCommand for collection injection
builder.Services.AddSingleton<IDynamoDbCommand, CreateTableCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, DeleteTableCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, DescribeTableCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, ListTablesCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, PutItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, GetItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, DeleteItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, UpdateItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, QueryCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, ScanCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, BatchGetItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, BatchWriteItemCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, TransactWriteItemsCommand>();
builder.Services.AddSingleton<IDynamoDbCommand, TransactGetItemsCommand>();

builder.Services.AddSingleton<DynamoDbRequestRouter>();

// ... existing port configuration (unchanged)

var app = builder.Build();

var healthCheckOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
    }
};
app.MapHealthChecks("/", healthCheckOptions);
app.MapHealthChecks("/healthz", healthCheckOptions);

app.UseMiddleware<DynamoDbErrorMiddleware>();
app.UseMiddleware<DynamoDbValidationMiddleware>();

var router = app.Services.GetRequiredService<DynamoDbRequestRouter>();
app.MapPost("/", async context => await router.HandleRequest(context));

app.Run();

public partial class Program { }
```

**Middleware order matters:**
1. `MapHealthChecks("/", "/healthz")` — `GET` health checks handled first, never hit DynamoDB middleware
2. `DynamoDbErrorMiddleware` — outermost wrapper, catches all exceptions from downstream
3. `DynamoDbValidationMiddleware` — validates request, calls next
4. `MapPost("/", router.HandleRequest)` — terminal handler, dispatches to command

## Files Added

| File | Contents |
|------|----------|
| `Middleware/DynamoDbErrorMiddleware.cs` | Error-handling middleware with `WriteError` and `TransactionCanceledException` support |
| `Middleware/DynamoDbValidationMiddleware.cs` | Request validation middleware: POST method, path, X-Amz-Target header parsing |
| `Commands/IDynamoDbCommand.cs` | `IDynamoDbCommand` interface + `DynamoDbCommand<TRequest, TResponse>` abstract base class |
| `Commands/TableCommands.cs` | `CreateTableCommand`, `DeleteTableCommand`, `DescribeTableCommand`, `ListTablesCommand` |
| `Commands/ItemCommands.cs` | `PutItemCommand`, `GetItemCommand`, `DeleteItemCommand`, `UpdateItemCommand` |
| `Commands/QueryScanCommands.cs` | `QueryCommand`, `ScanCommand` |
| `Commands/BatchCommands.cs` | `BatchGetItemCommand`, `BatchWriteItemCommand` |
| `Commands/TransactionCommands.cs` | `TransactWriteItemsCommand`, `TransactGetItemsCommand` |

## Files Modified

| File | Change |
|------|--------|
| `Middleware/DynamoDbRequestRouter.cs` | Replace 5-parameter constructor + switch + `Dispatch<TReq,TRes>` with `IEnumerable<IDynamoDbCommand>` dictionary dispatch |
| `Program.cs` | Add health check services, register 14 commands as `IDynamoDbCommand`, wire middleware pipeline |

## Files Removed

None.

## Migration Strategy

The refactoring is done incrementally, one extraction at a time, with tests passing after each step:

1. Extract health check to `MapHealthChecks` in `Program.cs` — tests pass
2. Extract error formatting to `DynamoDbErrorMiddleware`, remove catch blocks and `WriteError` from router — tests pass
3. Extract validation to `DynamoDbValidationMiddleware`, remove header parsing from router — tests pass
4. Introduce `IDynamoDbCommand` interface and abstract base class
5. Create command classes one group at a time (Table → Item → QueryScan → Batch → Transaction), replacing switch arms as each group is complete — tests pass after each group
6. Replace router constructor and switch with dictionary lookup — tests pass
7. Final cleanup: verify router has no operation-specific knowledge, run full test suite
