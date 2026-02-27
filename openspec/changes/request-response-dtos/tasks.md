# Tasks

## Phase 1: Shared DTOs and Serializer Configuration
- [ ] Create `Models/SharedDtos.cs` with `KeySchemaElementDto`, `AttributeDefinitionDto`, `ProjectionDto`, `ProvisionedThroughputDto`, `ConsumedCapacityDto`, `LocalSecondaryIndexDto`, `GlobalSecondaryIndexDto`
- [ ] Configure shared `JsonSerializerOptions` with `AttributeValueConverter`, `WhenWritingNull`, and no automatic naming policy
- [ ] Verify `dotnet build` succeeds

## Phase 2: Table Operation DTOs
- [ ] Create `Models/TableRequests.cs` — `CreateTableRequest`, `DeleteTableRequest`, `DescribeTableRequest`, `ListTablesRequest`, `UpdateTableRequest`
- [ ] Create `Models/TableResponses.cs` — `CreateTableResponse`, `DeleteTableResponse`, `DescribeTableResponse`, `ListTablesResponse`, `UpdateTableResponse`, `TableDescriptionDto`
- [ ] Refactor `TableOperations` methods to accept request DTOs and return response DTOs
- [ ] Add `TableDefinition` → `TableDescriptionDto` mapping method to replace `WriteTableDescription()` helper
- [ ] Update `DynamoDbRequestRouter` table operation dispatch to deserialize/serialize DTOs
- [ ] Remove inline JSON parsing helpers from `TableOperations` (`ParseKeySchema`, `ParseAttributeDefinitions`, `ParseProjection`, `ParseLocalSecondaryIndexes`, `ParseGlobalSecondaryIndexes`)
- [ ] Run tests — all existing table tests must pass

## Phase 3: Item Operation DTOs
- [ ] Create `Models/ItemRequests.cs` — `PutItemRequest`, `GetItemRequest`, `DeleteItemRequest`, `UpdateItemRequest`
- [ ] Create `Models/ItemResponses.cs` — `PutItemResponse`, `GetItemResponse`, `DeleteItemResponse`, `UpdateItemResponse`
- [ ] Refactor `ItemOperations` methods to accept request DTOs and return response DTOs
- [ ] Update `DynamoDbRequestRouter` item operation dispatch
- [ ] Remove `DeserializeItem()`, `DeserializeStringMap()`, `WriteItem()`, `WriteItemsList()`, `BuildItemResponse()`, `BuildGetItemResponse()`, `BuildEmptyResponse()` helper methods
- [ ] Run tests — all existing item tests must pass

## Phase 4: Query/Scan Operation DTOs
- [ ] Create `Models/QueryRequests.cs` — `QueryRequest`, `ScanRequest`
- [ ] Create `Models/QueryResponses.cs` — `QueryResponse`, `ScanResponse`
- [ ] Refactor `QueryScanOperations.Query()` to accept `QueryRequest` and return `QueryResponse`
- [ ] Refactor `QueryScanOperations.Scan()` to accept `ScanRequest` and return `ScanResponse`
- [ ] Update `DynamoDbRequestRouter` query/scan dispatch
- [ ] Run tests — all existing query/scan tests must pass

## Phase 5: Batch Operation DTOs
- [ ] Create `Models/BatchRequests.cs` — `BatchGetItemRequest`, `BatchWriteItemRequest`, `BatchGetTableRequest`, `PutRequest`, `DeleteRequest`, `WriteRequest`
- [ ] Create `Models/BatchResponses.cs` — `BatchGetItemResponse`, `BatchWriteItemResponse`
- [ ] Refactor `BatchOperations` methods to accept request DTOs and return response DTOs
- [ ] Update `DynamoDbRequestRouter` batch dispatch
- [ ] Run tests — all existing batch tests must pass

## Phase 6: Transaction Operation DTOs
- [ ] Create `Models/TransactionRequests.cs` — `TransactWriteItemsRequest`, `TransactGetItemsRequest`, `TransactWriteItem`, `TransactGetItem`, `Put`, `Delete`, `Update`, `ConditionCheck`, `Get`
- [ ] Create `Models/TransactionResponses.cs` — `TransactWriteItemsResponse`, `TransactGetItemsResponse`, `ItemResponse`
- [ ] Refactor `TransactionOperations` methods to accept request DTOs and return response DTOs
- [ ] Update `DynamoDbRequestRouter` transaction dispatch
- [ ] Run tests — all existing transaction tests must pass

## Phase 7: Router Cleanup
- [ ] Replace per-operation dispatch with generic `Dispatch<TReq, TRes>` pattern
- [ ] Remove `JsonDocument` parameter from all operation dispatch paths
- [ ] Verify no `Utf8JsonWriter` / `MemoryStream` / `JsonDocument.Parse()` response-building code remains in operation classes
- [ ] Run full test suite — `dotnet test` must pass with 0 failures
