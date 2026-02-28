# Tasks

## Phase 1: Extract Health Check
- [x] Add `builder.Services.AddHealthChecks()` to `Program.cs`
- [x] Add `app.MapHealthChecks("/", ...)` and `app.MapHealthChecks("/healthz", ...)` with shared custom response writer to `Program.cs`
- [x] Remove `GET /` handling from `DynamoDbRequestRouter.HandleRequest()`
- [x] Change `app.Map("/", ...)` to `app.MapPost("/", ...)` in `Program.cs`
- [x] Run tests — health check and all DynamoDB operations must pass

## Phase 2: Extract Error Handling
- [x] Create `src/MockDynamoDB.Server/Middleware/DynamoDbErrorMiddleware.cs` with `InvokeAsync`, `WriteError`, and `TransactionCanceledException` support
- [x] Add `app.UseMiddleware<DynamoDbErrorMiddleware>()` to `Program.cs` before the validation middleware
- [x] Remove `try/catch` blocks from `DynamoDbRequestRouter.HandleRequest()`
- [x] Remove `WriteError` method from `DynamoDbRequestRouter`
- [x] Run tests — all error scenarios must still return correct status codes and error types

## Phase 3: Extract Validation
- [x] Create `src/MockDynamoDB.Server/Middleware/DynamoDbValidationMiddleware.cs` with POST method check, path check, and `X-Amz-Target` header parsing
- [x] Add `app.UseMiddleware<DynamoDbValidationMiddleware>()` to `Program.cs` after error middleware
- [x] Remove HTTP method/path validation, header parsing, and `TargetPrefix` constant from `DynamoDbRequestRouter`
- [x] Run tests — missing header, unknown operation, and wrong method scenarios must pass

## Phase 4: Introduce Command Pattern
- [x] Create `src/MockDynamoDB.Server/Commands/IDynamoDbCommand.cs` with the `IDynamoDbCommand` interface and `DynamoDbCommand<TRequest, TResponse>` abstract base class
- [x] Create `Commands/TableCommands.cs` — `CreateTableCommand`, `DeleteTableCommand`, `DescribeTableCommand`, `ListTablesCommand`
- [x] Create `Commands/ItemCommands.cs` — `PutItemCommand`, `GetItemCommand`, `DeleteItemCommand`, `UpdateItemCommand`
- [x] Create `Commands/QueryScanCommands.cs` — `QueryCommand`, `ScanCommand`
- [x] Create `Commands/BatchCommands.cs` — `BatchGetItemCommand`, `BatchWriteItemCommand`
- [x] Create `Commands/TransactionCommands.cs` — `TransactWriteItemsCommand`, `TransactGetItemsCommand`
- [x] Register all 14 commands as `IDynamoDbCommand` in `Program.cs`
- [x] Replace router constructor parameters and switch expression with `IEnumerable<IDynamoDbCommand>` dictionary lookup
- [x] Remove `Dispatch<TReq, TRes>` from `DynamoDbRequestRouter`
- [x] Run tests — all 14 operations must pass

## Phase 5: Final Cleanup and Verification
- [x] Verify `DynamoDbRequestRouter` contains only: constructor building dictionary, header parsing, dictionary lookup, and response writing
- [x] Verify router has no imports of or references to any specific operation request/response types
- [x] Run `dotnet build MockDynamoDB.slnx` — 0 errors, 0 warnings
- [x] Run `dotnet test` — 0 failures
