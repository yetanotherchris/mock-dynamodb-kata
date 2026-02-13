# Tasks

## Phase 1: Test Infrastructure
- [x] Add `AWSSDK.DynamoDBv2` v4.x NuGet package to MockDynamoDB.Tests.Spec
- [x] Add `Microsoft.AspNetCore.Mvc.Testing` NuGet package for in-process hosting
- [x] Implement `MockDynamoDbFixture` with `WebApplicationFactory<Program>`
- [x] Implement `TestHttpClientFactory` to inject the test server's handler
- [x] Configure `DisableDangerousDisablePathAndQueryCanonicalization` for SDK v4

## Phase 2: Table Operation Tests
- [x] Test CreateTable with hash key returns ACTIVE status
- [x] Test CreateTable with hash and range key returns both key elements
- [x] Test CreateTable on existing table throws ResourceInUseException
- [x] Test DescribeTable returns table info
- [x] Test DescribeTable on non-existent table throws ResourceNotFoundException
- [x] Test DeleteTable removes table and subsequent describe throws
- [x] Test DeleteTable on non-existent table throws ResourceNotFoundException
- [x] Test ListTables returns all created table names

## Phase 3: Item CRUD Tests
- [x] Test PutItem and GetItem roundtrip returns stored item
- [x] Test GetItem for non-existent key returns IsItemSet = false
- [x] Test PutItem replaces existing item with same key
- [x] Test DeleteItem removes item
- [x] Test DeleteItem with ReturnValues = ALL_OLD returns deleted attributes
- [x] Test PutItem with all attribute types (S, N, BOOL, NULL, L, M, SS, NS)
- [x] Test PutItem on non-existent table throws ResourceNotFoundException

## Phase 4: Query Tests
- [x] Test Query by partition key only
- [x] Test Query with begins_with on sort key
- [x] Test Query with BETWEEN on sort key
- [x] Test Query in reverse order (ScanIndexForward = false)
- [x] Test Query with FilterExpression
- [x] Test Query with Limit and LastEvaluatedKey

## Phase 5: Scan Tests
- [x] Test Scan all items in table
- [x] Test Scan with FilterExpression
- [x] Test Scan with Limit
- [x] Test Scan pagination using ExclusiveStartKey

## Phase 6: UpdateItem Tests
- [x] Test SET attribute to new value
- [x] Test arithmetic update (SET #c = #c + :inc)
- [x] Test REMOVE attribute
- [x] Test ConditionExpression failure throws ConditionalCheckFailedException
- [x] Test ReturnValues = ALL_NEW returns updated item
- [x] Test upsert (update on non-existent key creates item)
- [x] Test ADD to string set
- [x] Test DELETE from string set

## Phase 7: Batch Operation Tests
- [x] Test BatchWriteItem with multiple put requests
- [x] Test BatchGetItem retrieves existing items (ignores non-existent keys)

## Phase 8: Transaction Tests
- [x] Test TransactWriteItems all succeed atomically
- [x] Test TransactWriteItems with failed condition check (none applied)
- [x] Test TransactGetItems returns items for existing keys and empty for missing

## Phase 9: LSI Tests
- [x] Test Query on LSI returns items sorted by index sort key
- [x] Test Query on LSI with sort key condition
- [x] Test Query on LSI excludes items without the index sort key attribute
- [x] Test Query on LSI in reverse order
- [x] Test Query on LSI returns all projected attributes (ProjectionType.ALL)
- [x] Test Query on non-existent index throws AmazonDynamoDBException

## Phase 10: CI/CD
- [x] Enable GitHub Actions workflow triggers for PR and push to main
- [x] Verify workflow runs build, unit tests, and integration tests
