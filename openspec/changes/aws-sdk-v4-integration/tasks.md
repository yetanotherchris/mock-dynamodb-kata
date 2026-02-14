# Tasks

## Phase 1: Test Infrastructure (In-Process Test Hosting + AWS SDK v4 Compatibility)
- [x] Add `AWSSDK.DynamoDBv2` v4.x NuGet package to MockDynamoDB.Tests.Spec
- [x] Add `Microsoft.AspNetCore.Mvc.Testing` NuGet package for in-process hosting
- [x] Implement `MockDynamoDbFixture` with `WebApplicationFactory<Program>` — *Scenario: Server starts via WebApplicationFactory*
- [x] Implement `TestHttpClientFactory` to inject the test server's handler — *Scenario: SDK client connects through test handler*
- [x] Configure `DisableDangerousDisablePathAndQueryCanonicalization` for SDK v4 — *Scenario: Path canonicalization disabled*

## Phase 2: Table Operation Tests (Table Operations via SDK)
- [x] Test CreateTable with hash key returns ACTIVE status — *Scenario: Create a hash-key table*
- [x] Test CreateTable with hash and range key returns both key elements — *Scenario: Create a composite-key table*
- [x] Test CreateTable on existing table throws ResourceInUseException — *Scenario: Create duplicate table*
- [x] Test DescribeTable returns table info
- [x] Test DescribeTable on non-existent table throws ResourceNotFoundException
- [x] Test DeleteTable removes table and subsequent describe throws — *Scenario: Delete a table*
- [x] Test DeleteTable on non-existent table throws ResourceNotFoundException
- [x] Test ListTables returns all created table names — *Scenario: List tables*

## Phase 3: Item CRUD Tests (Item CRUD via SDK)
- [x] Test PutItem and GetItem roundtrip returns stored item — *Scenario: Put and get an item*
- [x] Test GetItem for non-existent key returns IsItemSet = false — *Scenario: Get non-existent item*
- [x] Test PutItem replaces existing item with same key — *Scenario: Replace existing item*
- [x] Test DeleteItem removes item — *Scenario: Delete an item*
- [x] Test DeleteItem with ReturnValues = ALL_OLD returns deleted attributes — *Scenario: Delete with return values*
- [x] Test PutItem with all attribute types (S, N, BOOL, NULL, L, M, SS, NS) — *Scenario: All attribute types*
- [x] Test PutItem on non-existent table throws ResourceNotFoundException — *Scenario: Operation on non-existent table*

## Phase 4: Query Tests (Query Operations via SDK)
- [x] Test Query by partition key only — *Scenario: Query by partition key*
- [x] Test Query with begins_with on sort key — *Scenario: Query with begins_with on sort key*
- [x] Test Query with BETWEEN on sort key — *Scenario: Query with BETWEEN on sort key*
- [x] Test Query in reverse order (ScanIndexForward = false) — *Scenario: Query in reverse order*
- [x] Test Query with FilterExpression — *Scenario: Query with filter expression*
- [x] Test Query with Limit and LastEvaluatedKey — *Scenario: Query with limit*

## Phase 5: Scan Tests (Scan Operations via SDK)
- [x] Test Scan all items in table — *Scenario: Scan all items*
- [x] Test Scan with FilterExpression — *Scenario: Scan with filter expression*
- [x] Test Scan with Limit — *Scenario: Scan with limit*
- [x] Test Scan pagination using ExclusiveStartKey — *Scenario: Scan with pagination*

## Phase 6: UpdateItem Tests (UpdateItem via SDK)
- [x] Test SET attribute to new value — *Scenario: SET attribute*
- [x] Test arithmetic update (SET #c = #c + :inc) — *Scenario: Arithmetic update*
- [x] Test REMOVE attribute — *Scenario: REMOVE attribute*
- [x] Test ConditionExpression failure throws ConditionalCheckFailedException — *Scenario: Condition check fails*
- [x] Test ReturnValues = ALL_NEW returns updated item — *Scenario: Return ALL_NEW*
- [x] Test upsert (update on non-existent key creates item) — *Scenario: Upsert (update non-existent item)*
- [x] Test ADD to string set — *Scenario: ADD to string set*
- [x] Test DELETE from string set — *Scenario: DELETE from string set*

## Phase 7: Batch Operation Tests (Batch Operations via SDK)
- [x] Test BatchWriteItem with multiple put requests — *Scenario: BatchWriteItem puts multiple items*
- [x] Test BatchGetItem retrieves existing items (ignores non-existent keys) — *Scenario: BatchGetItem retrieves multiple items*

## Phase 8: Transaction Tests (Transaction Operations via SDK)
- [x] Test TransactWriteItems all succeed atomically — *Scenario: TransactWriteItems all succeed*
- [x] Test TransactWriteItems with failed condition check (none applied) — *Scenario: TransactWriteItems condition fails*
- [x] Test TransactGetItems returns items for existing keys and empty for missing — *Scenario: TransactGetItems retrieves items*

## Phase 9: LSI Tests (Local Secondary Index via SDK)
- [x] Test Query on LSI returns items sorted by index sort key — *Scenario: Query on LSI returns items sorted by index sort key*
- [x] Test Query on LSI with sort key condition — *Scenario: Query on LSI with sort key condition*
- [x] Test Query on LSI excludes items without the index sort key attribute
- [x] Test Query on LSI in reverse order — *Scenario: Query on LSI in reverse order*
- [x] Test Query on LSI returns all projected attributes (ProjectionType.ALL) — *Scenario: Query on LSI returns all projected attributes*
- [x] Test Query on non-existent index throws AmazonDynamoDBException — *Scenario: Query on non-existent index*

## Phase 10: CI/CD (CI/CD Integration)
- [x] Enable GitHub Actions workflow triggers for PR and push to main — *Scenario: Tests run on pull requests + Tests run on main branch pushes*
- [x] Verify workflow runs build, unit tests, and integration tests — *Scenario: Tests require no external services*
