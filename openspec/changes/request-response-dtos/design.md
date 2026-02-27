# Design

## Architecture

The change introduces a DTO layer between HTTP serialization and business logic:

```
Before:  HTTP body → JsonDocument → Operation(JsonDocument) → Utf8JsonWriter → JsonDocument → HTTP body
After:   HTTP body → JsonSerializer.Deserialize<TRequest> → Operation(TRequest) → TResponse → JsonSerializer.Serialize → HTTP body
```

The router becomes the single serialization/deserialization boundary. Operation classes work entirely with typed C# objects.

## DTO Location

All request/response types live in `src/MockDynamoDB.Core/Models/` alongside the existing `AttributeValue`, `TableDefinition`, and `DynamoDbError` types.

DTOs are organised into files by operation group:

| File | Types |
|------|-------|
| `TableRequests.cs` | `CreateTableRequest`, `DeleteTableRequest`, `DescribeTableRequest`, `ListTablesRequest`, `UpdateTableRequest` |
| `TableResponses.cs` | `CreateTableResponse`, `DeleteTableResponse`, `DescribeTableResponse`, `ListTablesResponse`, `UpdateTableResponse`, `TableDescriptionDto` |
| `ItemRequests.cs` | `PutItemRequest`, `GetItemRequest`, `DeleteItemRequest`, `UpdateItemRequest` |
| `ItemResponses.cs` | `PutItemResponse`, `GetItemResponse`, `DeleteItemResponse`, `UpdateItemResponse` |
| `QueryRequests.cs` | `QueryRequest`, `ScanRequest` |
| `QueryResponses.cs` | `QueryResponse`, `ScanResponse` |
| `BatchRequests.cs` | `BatchGetItemRequest`, `BatchWriteItemRequest`, `BatchGetTableRequest`, `PutRequest`, `DeleteRequest`, `WriteRequest` |
| `BatchResponses.cs` | `BatchGetItemResponse`, `BatchWriteItemResponse` |
| `TransactionRequests.cs` | `TransactWriteItemsRequest`, `TransactGetItemsRequest`, `TransactWriteItem`, `TransactGetItem`, `Put`, `Delete`, `Update`, `ConditionCheck`, `Get` |
| `TransactionResponses.cs` | `TransactWriteItemsResponse`, `TransactGetItemsResponse`, `ItemResponse` |

## DTO Style

All DTOs are C# `record` types with `JsonPropertyName` attributes matching the DynamoDB wire protocol field names. Optional fields use nullable types.

```csharp
public sealed record PutItemRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Item")]
    public required Dictionary<string, AttributeValue> Item { get; init; }

    [JsonPropertyName("ReturnValues")]
    public string? ReturnValues { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("Expected")]
    public Dictionary<string, JsonElement>? Expected { get; init; }
}

public sealed record PutItemResponse
{
    [JsonPropertyName("Attributes")]
    public Dictionary<string, AttributeValue>? Attributes { get; init; }
}
```

## Shared Types

Recurring structures extracted into reusable records:

| Type | Fields | Used By |
|------|--------|---------|
| `KeySchemaElementDto` | `AttributeName`, `KeyType` | CreateTable, UpdateTable |
| `AttributeDefinitionDto` | `AttributeName`, `AttributeType` | CreateTable, UpdateTable |
| `ProjectionDto` | `ProjectionType`, `NonKeyAttributes?` | CreateTable, UpdateTable, DescribeTable |
| `ProvisionedThroughputDto` | `ReadCapacityUnits`, `WriteCapacityUnits` | CreateTable, UpdateTable, DescribeTable |
| `ConsumedCapacityDto` | `TableName`, `CapacityUnits` | Query |
| `LocalSecondaryIndexDto` | `IndexName`, `KeySchema`, `Projection` | CreateTable, DescribeTable |
| `GlobalSecondaryIndexDto` | `IndexName`, `KeySchema`, `Projection`, `ProvisionedThroughput?` | CreateTable, UpdateTable, DescribeTable |

These shared types may overlap with fields on `TableDefinition`. Where possible, `TableDefinition` properties can be reused; otherwise the DTOs exist as a separate serialization-focused layer.

## Router Changes

`DynamoDbRequestRouter` changes from:

```csharp
using var body = await JsonDocument.ParseAsync(context.Request.Body);
var result = DispatchOperation(operation, body);
// write result JsonDocument to response
```

To:

```csharp
var result = operation switch
{
    "CreateTable" => Dispatch<CreateTableRequest, CreateTableResponse>(body, tableOps.CreateTable),
    "PutItem"     => Dispatch<PutItemRequest, PutItemResponse>(body, itemOps.PutItem),
    // ...
};

private static async Task<byte[]> Dispatch<TReq, TRes>(Stream body, Func<TReq, TRes> handler)
{
    var request = await JsonSerializer.DeserializeAsync<TReq>(body, JsonOptions);
    var response = handler(request!);
    return JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
}
```

A shared `JsonSerializerOptions` instance with `DefaultIgnoreCondition = WhenWritingNull` ensures optional response fields are omitted when null, matching the current behavior.

## Operation Class Changes

Each operation method signature changes:

```csharp
// Before
public JsonDocument PutItem(JsonDocument request) { ... }

// After
public PutItemResponse PutItem(PutItemRequest request) { ... }
```

The method bodies simplify from:

```csharp
var root = request.RootElement;
var tableName = root.GetProperty("TableName").GetString()!;
var item = DeserializeItem(root.GetProperty("Item"));
```

To:

```csharp
var table = tableStore.GetTable(request.TableName);
var item = request.Item;
```

## Response Serialization

The `Utf8JsonWriter` → `MemoryStream` → `JsonDocument.Parse()` pattern is eliminated entirely. Response DTOs are serialized by the router via `JsonSerializer.SerializeToUtf8Bytes()`.

The `TableDescription` response (used by CreateTable, DeleteTable, DescribeTable, UpdateTable) is built by mapping `TableDefinition` to a `TableDescriptionDto` record — this mapping logic replaces the current `WriteTableDescription()` helper method.

## JSON Serializer Options

A single shared `JsonSerializerOptions` instance configured with:

- The existing `AttributeValueConverter` (for `AttributeValue` serialization)
- `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (omit null optional fields)
- `PropertyNamingPolicy = null` (use explicit `JsonPropertyName` attributes, no automatic casing)

## Error Handling

Error responses continue to use the existing `DynamoDbException` hierarchy. The router's `catch` block remains unchanged — exceptions are caught and serialized to the DynamoDB error format (`__type` + `Message`) as before.

## Files Modified

| File | Change |
|------|--------|
| `DynamoDbRequestRouter.cs` | Replace `JsonDocument` dispatch with generic `Dispatch<TReq, TRes>` |
| `TableOperations.cs` | Accept/return typed DTOs; remove `WriteTableDescription()`, parsing helpers |
| `ItemOperations.cs` | Accept/return typed DTOs; remove `DeserializeItem()`, `WriteItem()`, response builders |
| `QueryScanOperations.cs` | Accept/return typed DTOs; remove JSON navigation |
| `BatchOperations.cs` | Accept/return typed DTOs; remove JSON navigation |
| `TransactionOperations.cs` | Accept/return typed DTOs; remove JSON navigation |

## Files Added

| File | Contents |
|------|----------|
| `Models/TableRequests.cs` | Table operation request records |
| `Models/TableResponses.cs` | Table operation response records + `TableDescriptionDto` |
| `Models/ItemRequests.cs` | Item operation request records |
| `Models/ItemResponses.cs` | Item operation response records |
| `Models/QueryRequests.cs` | Query and Scan request records |
| `Models/QueryResponses.cs` | Query and Scan response records |
| `Models/BatchRequests.cs` | Batch operation request records |
| `Models/BatchResponses.cs` | Batch operation response records |
| `Models/TransactionRequests.cs` | Transaction operation request records |
| `Models/TransactionResponses.cs` | Transaction operation response records |
| `Models/SharedDtos.cs` | Shared types: `KeySchemaElementDto`, `ProjectionDto`, `ProvisionedThroughputDto`, etc. |

## Files Removed

No files are removed. Inline helper methods within operation classes are deleted as part of the refactoring.

## Migration Strategy

The refactoring is done operation-group-at-a-time to keep each step compilable and testable:

1. Define shared DTOs and serializer options
2. Refactor table operations (simplest, fewest fields)
3. Refactor item operations (introduces `AttributeValue` map serialization)
4. Refactor query/scan operations (most complex request shapes)
5. Refactor batch operations (nested per-table structures)
6. Refactor transaction operations (polymorphic action types)
7. Clean up router to use generic dispatch
