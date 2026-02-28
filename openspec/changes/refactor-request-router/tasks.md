# Tasks

## Phase 1: Extract Health Check
- [ ] Add `builder.Services.AddHealthChecks()` to `Program.cs`
- [ ] Add `app.MapHealthChecks("/", ...)` and `app.MapHealthChecks("/healthz", ...)` with shared custom response writer to `Program.cs`
- [ ] Remove `GET /` handling from `DynamoDbRequestRouter.HandleRequest()`
- [ ] Change `app.Map("/", ...)` to `app.MapPost("/", ...)` in `Program.cs`
- [ ] Run tests — health check and all DynamoDB operations must pass

## Phase 2: Extract Error Handling
- [ ] Create `src/MockDynamoDB.Server/Middleware/DynamoDbErrorMiddleware.cs` with `InvokeAsync`, `WriteError`, and `TransactionCanceledException` support
- [ ] Add `app.UseMiddleware<DynamoDbErrorMiddleware>()` to `Program.cs` before the validation middleware
- [ ] Remove `try/catch` blocks from `DynamoDbRequestRouter.HandleRequest()`
- [ ] Remove `WriteError` method from `DynamoDbRequestRouter`
- [ ] Run tests — all error scenarios must still return correct status codes and error types

## Phase 3: Extract Validation
- [ ] Create `src/MockDynamoDB.Server/Middleware/DynamoDbValidationMiddleware.cs` with POST method check, path check, and `X-Amz-Target` header parsing
- [ ] Add `app.UseMiddleware<DynamoDbValidationMiddleware>()` to `Program.cs` after error middleware
- [ ] Remove HTTP method/path validation, header parsing, and `TargetPrefix` constant from `DynamoDbRequestRouter`
- [ ] Run tests — missing header, unknown operation, and wrong method scenarios must pass

## Phase 4: Introduce Command Pattern
- [ ] Create `src/MockDynamoDB.Server/Commands/IDynamoDbCommand.cs` with the `IDynamoDbCommand` interface and `DynamoDbCommand<TRequest, TResponse>` abstract base class
- [ ] Create `Commands/TableCommands.cs` — `CreateTableCommand`, `DeleteTableCommand`, `DescribeTableCommand`, `ListTablesCommand`
- [ ] Create `Commands/ItemCommands.cs` — `PutItemCommand`, `GetItemCommand`, `DeleteItemCommand`, `UpdateItemCommand`
- [ ] Create `Commands/QueryScanCommands.cs` — `QueryCommand`, `ScanCommand`
- [ ] Create `Commands/BatchCommands.cs` — `BatchGetItemCommand`, `BatchWriteItemCommand`
- [ ] Create `Commands/TransactionCommands.cs` — `TransactWriteItemsCommand`, `TransactGetItemsCommand`
- [ ] Register all 14 commands as `IDynamoDbCommand` in `Program.cs`
- [ ] Replace router constructor parameters and switch expression with `IEnumerable<IDynamoDbCommand>` dictionary lookup
- [ ] Remove `Dispatch<TReq, TRes>` from `DynamoDbRequestRouter`
- [ ] Run tests — all 14 operations must pass

## Phase 5: Final Cleanup and Verification
- [ ] Verify `DynamoDbRequestRouter` contains only: constructor building dictionary, header parsing, dictionary lookup, and response writing
- [ ] Verify router has no imports of or references to any specific operation request/response types
- [ ] Run `dotnet build MockDynamoDB.slnx` — 0 errors, 0 warnings
- [ ] Run `dotnet test` — 0 failures
