# Design

## Architecture

The current monolithic request handler is decomposed into an ASP.NET Core middleware pipeline:

```
Before:
  HTTP Request → DynamoDbRequestRouter.HandleRequest() → (does everything) → HTTP Response

After:
  HTTP Request
    → MapHealthChecks "/" (short-circuits GET health check requests)
    → DynamoDbErrorMiddleware (wraps downstream in try/catch, formats error responses)
    → DynamoDbValidationMiddleware (validates POST /, parses X-Amz-Target, short-circuits invalid requests)
    → DynamoDbRequestRouter (pure dispatch: switch → Dispatch<TReq, TRes> → write response)
```

Each middleware either short-circuits (returns a response directly) or calls `next()` to pass control downstream. This follows ASP.NET Core's standard middleware pattern.

## Health Check

Uses ASP.NET Core's built-in health check framework (`Microsoft.Extensions.Diagnostics.HealthChecks`, included in the shared framework — no extra NuGet needed). Mapped to `GET /` with a custom response writer to match the existing JSON body.

```csharp
// Program.cs — DI registration
builder.Services.AddHealthChecks();

// Program.cs — endpoint mapping
app.MapHealthChecks("/", new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
    }
});
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

**Why `HttpContext.Items`?** This is ASP.NET Core's built-in mechanism for passing per-request data between middleware components. The router reads the operation name from `context.Items["DynamoDb.Operation"]` instead of re-parsing the header.

**404 for non-POST requests:** The 404 response for wrong method/path is not a DynamoDB error — it's an HTTP-level concern. Writing the status code directly (without throwing) is correct because `DynamoDbErrorMiddleware` should only handle DynamoDB-specific errors.

## Changed Classes

### `Middleware/DynamoDbRequestRouter.cs`

The router is stripped to its single remaining responsibility: dispatch an operation name to the correct typed handler.

```csharp
namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbRequestRouter(
    TableOperations tableOps,
    ItemOperations itemOps,
    QueryScanOperations queryScanOps,
    BatchOperations batchOps,
    TransactionOperations txOps)
{
    private static readonly JsonSerializerOptions JsonOptions = DynamoDbJsonOptions.Options;

    public async Task HandleRequest(HttpContext context)
    {
        var operation = (string)context.Items["DynamoDb.Operation"]!;

        var result = operation switch
        {
            "CreateTable" => await Dispatch<CreateTableRequest, CreateTableResponse>(context.Request.Body, tableOps.CreateTable),
            "DeleteTable" => await Dispatch<DeleteTableRequest, DeleteTableResponse>(context.Request.Body, tableOps.DeleteTable),
            "DescribeTable" => await Dispatch<DescribeTableRequest, DescribeTableResponse>(context.Request.Body, tableOps.DescribeTable),
            "ListTables" => await Dispatch<ListTablesRequest, ListTablesResponse>(context.Request.Body, tableOps.ListTables),
            "PutItem" => await Dispatch<PutItemRequest, PutItemResponse>(context.Request.Body, itemOps.PutItem),
            "GetItem" => await Dispatch<GetItemRequest, GetItemResponse>(context.Request.Body, itemOps.GetItem),
            "DeleteItem" => await Dispatch<DeleteItemRequest, DeleteItemResponse>(context.Request.Body, itemOps.DeleteItem),
            "UpdateItem" => await Dispatch<UpdateItemRequest, UpdateItemResponse>(context.Request.Body, itemOps.UpdateItem),
            "Query" => await Dispatch<QueryRequest, QueryResponse>(context.Request.Body, queryScanOps.Query),
            "Scan" => await Dispatch<ScanRequest, ScanResponse>(context.Request.Body, queryScanOps.Scan),
            "BatchGetItem" => await Dispatch<BatchGetItemRequest, BatchGetItemResponse>(context.Request.Body, batchOps.BatchGetItem),
            "BatchWriteItem" => await Dispatch<BatchWriteItemRequest, BatchWriteItemResponse>(context.Request.Body, batchOps.BatchWriteItem),
            "TransactWriteItems" => await Dispatch<TransactWriteItemsRequest, TransactWriteItemsResponse>(context.Request.Body, txOps.TransactWriteItems),
            "TransactGetItems" => await Dispatch<TransactGetItemsRequest, TransactGetItemsResponse>(context.Request.Body, txOps.TransactGetItems),
            _ => throw new UnknownOperationException()
        };

        context.Response.ContentType = "application/x-amz-json-1.0";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(result);
    }

    private static async Task<byte[]> Dispatch<TReq, TRes>(Stream body, Func<TReq, TRes> handler)
    {
        var request = await JsonSerializer.DeserializeAsync<TReq>(body, JsonOptions);
        var response = handler(request!);
        return JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
    }
}
```

**What was removed:**
- Health check handling (`GET /` check and response) — now in `HealthCheckHandler`
- HTTP method/path validation — now in `DynamoDbValidationMiddleware`
- `X-Amz-Target` header parsing and validation — now in `DynamoDbValidationMiddleware`
- `WriteError` method and all catch blocks — now in `DynamoDbErrorMiddleware`
- `TargetPrefix` constant — moved to `DynamoDbValidationMiddleware`

**What remains:**
- Switch expression mapping operation names to typed handlers
- `Dispatch<TReq, TRes>` for JSON deserialization/serialization
- Writing the 200 response with correct content type

### `Program.cs`

The middleware pipeline is wired in order. Endpoint routing (`MapHealthChecks`, `MapPost`) runs first, then middleware applies to the DynamoDB POST endpoint.

```csharp
using MockDynamoDB.Server.Middleware;
// ... existing usings

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// Existing DI registrations (unchanged)
builder.Services.AddSingleton<ITableStore, InMemoryTableStore>();
builder.Services.AddSingleton<IItemStore, InMemoryItemStore>();
builder.Services.AddSingleton<ReaderWriterLockSlim>();
builder.Services.AddSingleton<TableOperations>();
builder.Services.AddSingleton<ItemOperations>();
builder.Services.AddSingleton<QueryScanOperations>();
builder.Services.AddSingleton<BatchOperations>();
builder.Services.AddSingleton<TransactionOperations>();
builder.Services.AddSingleton<DynamoDbRequestRouter>();

// ... existing port configuration (unchanged)

var app = builder.Build();

app.MapHealthChecks("/", new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
    }
});

app.UseMiddleware<DynamoDbErrorMiddleware>();
app.UseMiddleware<DynamoDbValidationMiddleware>();

var router = app.Services.GetRequiredService<DynamoDbRequestRouter>();
app.MapPost("/", async context => await router.HandleRequest(context));

app.Run();

public partial class Program { }
```

**Middleware order matters:**
1. `MapHealthChecks("/", ...)` — `GET /` handled first via endpoint routing, never hits DynamoDB middleware
2. `DynamoDbErrorMiddleware` — outermost wrapper, catches all exceptions from downstream
3. `DynamoDbValidationMiddleware` — validates request, stores operation name, calls next
4. `MapPost("/", router.HandleRequest)` — terminal handler, dispatches operation

## Files Added

| File | Contents |
|------|----------|
| `Middleware/DynamoDbErrorMiddleware.cs` | Error-handling middleware with `WriteError` and `TransactionCanceledException` support |
| `Middleware/DynamoDbValidationMiddleware.cs` | Request validation middleware: POST method, path, X-Amz-Target header parsing |

## Files Modified

| File | Change |
|------|--------|
| `Middleware/DynamoDbRequestRouter.cs` | Remove health check, validation, error handling; read operation from `HttpContext.Items` |
| `Program.cs` | Add `AddHealthChecks()`, `MapHealthChecks("/")`, wire `DynamoDbErrorMiddleware` and `DynamoDbValidationMiddleware` |

## Files Removed

None.

## Migration Strategy

The refactoring is done incrementally, one extraction at a time, with tests passing after each step:

1. Extract health check to `HealthCheckHandler`, update `Program.cs` routing — tests pass
2. Extract error formatting to `DynamoDbErrorMiddleware`, remove catch blocks and `WriteError` from router — tests pass
3. Extract validation to `DynamoDbValidationMiddleware`, remove header parsing from router — tests pass
4. Final cleanup: remove dead code, verify router is minimal — full test suite passes
