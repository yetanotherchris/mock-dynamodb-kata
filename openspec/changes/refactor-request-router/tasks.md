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
- [ ] Update `DynamoDbRequestRouter.HandleRequest()` to read operation name from `X-Amz-Target` header directly (validation middleware guarantees it's valid)
- [ ] Remove HTTP method/path validation from `DynamoDbRequestRouter`
- [ ] Remove `X-Amz-Target` header parsing from `DynamoDbRequestRouter`
- [ ] Remove `TargetPrefix` constant from `DynamoDbRequestRouter`
- [ ] Run tests — missing header, unknown operation, and wrong method scenarios must pass

## Phase 4: Final Cleanup and Verification
- [ ] Verify `DynamoDbRequestRouter` contains only: switch dispatch, `Dispatch<TReq, TRes>`, and response writing
- [ ] Verify no `WriteError` or health check code remains in router
- [ ] Run `dotnet build MockDynamoDB.slnx` — 0 errors, 0 warnings
- [ ] Run `dotnet test` — 0 failures
