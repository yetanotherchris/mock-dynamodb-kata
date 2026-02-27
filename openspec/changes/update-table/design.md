# Design

## Architecture

`UpdateTable` fits naturally into the existing layered architecture:

```
DynamoDbRequestRouter  →  TableOperations.UpdateTable()  →  ITableStore.UpdateTable()
                                                              (mutates TableDefinition in-place)
```

No new classes are needed. The operation is added alongside `CreateTable`, `DeleteTable`, `DescribeTable`, and `ListTables`.

## Request / Response Shape

**Request** (`UpdateTableRequest`):
```json
{
  "TableName": "MyTable",
  "BillingMode": "PROVISIONED",
  "ProvisionedThroughput": { "ReadCapacityUnits": 10, "WriteCapacityUnits": 5 },
  "GlobalSecondaryIndexUpdates": [
    { "Create": { "IndexName": "...", "KeySchema": [...], "Projection": {...}, "ProvisionedThroughput": {...} } },
    { "Update": { "IndexName": "...", "ProvisionedThroughput": {...} } },
    { "Delete": { "IndexName": "..." } }
  ]
}
```

**Response** (`UpdateTableResponse`):
```json
{
  "TableDescription": { ... }
}
```

## TableDefinition Changes

`TableDefinition` already holds `BillingMode`, `ProvisionedThroughput`, and a `GlobalSecondaryIndexes` list. `UpdateTable` mutates these fields under the existing write lock used by `ITableStore`.

## GSI Lifecycle

| Action | Behaviour |
|--------|-----------|
| Create | Appends a new `GlobalSecondaryIndexDescription` to the table; immediately scans existing items to populate the index (same logic as `CreateTable`) |
| Update | Replaces the `ProvisionedThroughput` on the named GSI |
| Delete | Removes the named GSI from the list; drops the associated sorted index structure |

## Error Handling

| Condition | Error |
|-----------|-------|
| Table does not exist | `ResourceNotFoundException` |
| GSI name in Update/Delete not found | `ValidationException` |
| GSI name in Create already exists | `ValidationException` |
| ProvisionedThroughput supplied when BillingMode is PAY_PER_REQUEST | `ValidationException` |

## Routing

`DynamoDbRequestRouter` dispatches on `X-Amz-Target` header suffix. A new branch is added:

```csharp
"UpdateTable" => await tableOperations.UpdateTable(body, ct),
```

## Test Organisation

New tests are added to `MockDynamoDB.Tests.Spec` in an `UpdateTableTests` class using the shared `MockDynamoDbFixture`:

| Test | Scenario |
|------|----------|
| `UpdateBillingMode_PayPerRequest` | Switch to PAY_PER_REQUEST, verify response |
| `UpdateProvisionedThroughput` | Change RCU/WCU on a PROVISIONED table |
| `UpdateTable_NonExistentTable` | Verify ResourceNotFoundException |
| `CreateGsi` | Add a new GSI, query it |
| `UpdateGsi_Throughput` | Update GSI provisioned throughput |
| `DeleteGsi` | Remove a GSI, verify query fails |
| `CreateGsi_DuplicateName` | Verify ValidationException |
| `DeleteGsi_NotFound` | Verify ValidationException |
