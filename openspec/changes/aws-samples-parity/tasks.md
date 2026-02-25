# Tasks

## Phase 1: Project Setup
- [ ] Create `tests/MockDynamoDB.Tests.Samples/MockDynamoDB.Tests.Samples.csproj` with dependencies (`AWSSDK.DynamoDBv2`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`, `TUnit`)
- [ ] Add the new project to `MockDynamoDB.slnx`
- [ ] Define `IMockBackend` interface with `Client` property and `IAsyncInitializer` / `IAsyncDisposable`

## Phase 2: MockDynamoDB Backend
- [ ] Implement `MockDynamoDbBackend` using `WebApplicationFactory<Program>` — mirrors `MockDynamoDbFixture` from the spec tests
- [ ] Configure `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true`
- [ ] Verify backend initialises and disposes cleanly

## Phase 3: Moto Backend
- [ ] Implement `MotoBackend` using `Testcontainers` to start `motoserver/moto:5.1.21`
- [ ] Configure container to expose DynamoDB port (default `5000`) on a random host port
- [ ] Add readiness wait strategy (HTTP GET `/` or `/moto-api/` returns 200)
- [ ] Build `AmazonDynamoDBClient` pointing to `http://localhost:<mappedPort>` with fake credentials

## Phase 4: WorkingWithTables Tests
- [ ] Test CreateTable with provisioned throughput (PK + SK) succeeds and returns ACTIVE status — *based on `CreateTableProvisioned.cs`*
- [ ] Test CreateTable with same name on same backend throws `ResourceInUseException`
- [ ] Test DeleteTable removes table — *verifying DescribeTable throws afterward*

## Phase 5: WorkingWithItems Tests
- [ ] Test PutItem stores item with nested map attribute (`address` map) — *based on `PutItem.cs` `RetailDatabase` example*
- [ ] Test GetItem retrieves stored item by `pk`/`sk` — *based on `GetItem.cs`*
- [ ] Test UpdateItem with `ExpressionAttributeNames` alias for reserved word `name` — *based on `UpdateItem.cs`*
- [ ] Test UpdateItemConditional succeeds when condition is met — *based on `UpdateItemConditional.cs`*
- [ ] Test UpdateItemConditional throws `ConditionalCheckFailedException` when condition fails — *based on `UpdateItemConditional.cs`*
- [ ] Test DeleteItem removes item — *based on `DeleteItem.cs`*
- [ ] Test DeleteItemConditional succeeds when condition is met — *based on `DeleteItemConditional.cs`*
- [ ] Test DeleteItemConditional throws `ConditionalCheckFailedException` when condition fails — *based on `DeleteItemConditional.cs`*
- [ ] Test PutItemConditional (`attribute_not_exists`) prevents overwrite of existing item — *based on `PutItemConditional.cs`*

## Phase 6: BatchItems Tests
- [ ] Test BatchWriteItem puts multiple items in one request — *based on `BatchWriteItem.cs`*
- [ ] Test BatchGetItem retrieves multiple items by key — *based on `BatchGetItem.cs`*
- [ ] Test BatchGetItem returns only existing keys (missing keys absent from response)

## Phase 7: TransactItems Tests
- [ ] Test TransactWriteItems writes multiple items atomically — *based on `TransactWriteItems.cs`*
- [ ] Test TransactWriteItems rolls back all when one condition fails — *based on `TransactWriteItems.cs` conditional example*
- [ ] Test TransactGetItems retrieves multiple items in a single request — *based on `TransactGetItems.cs`*

## Phase 8: WorkingWithQueries Tests
- [ ] Test Query with `FilterExpression` on non-key attribute — *based on `QueryFilterExpression.cs` (`CustomerName = :cn`)*
- [ ] Test Query with `ProjectionExpression` returns only specified attributes — *based on `QueryProjectionExpression.cs`*
- [ ] Test Query with `Select = COUNT` returns count without items — *based on `QueryCount.cs`*
- [ ] Test Query with `ConsistentRead = true` returns correct results — *based on `QueryConsistentRead.cs`*
- [ ] Test Query response includes `ConsumedCapacity` shape when `ReturnConsumedCapacity = TOTAL` is set — *based on `QueryConsumedCapacity.cs`*

## Phase 9: CI Integration
- [ ] Confirm existing GitHub Actions workflow (`build-and-test.yml`) picks up the new test project automatically (no change needed if it uses `dotnet test` at solution level)
- [ ] Document that Moto tests require Docker to be available on the CI runner
