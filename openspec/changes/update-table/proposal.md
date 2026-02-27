# UpdateTable

## Spec
`specs/tables`

## Problem
The mock DynamoDB server supports creating, describing, deleting, and listing tables but does not implement `UpdateTable`. Real AWS applications use `UpdateTable` to modify a table's billing mode, provisioned throughput, and Global Secondary Indexes at runtime. Without it, integration tests that call `UpdateTable` fail with an unrecognised operation error, and the mock cannot be used as a drop-in replacement for code that manages table configuration dynamically.

## Solution
Implement the `UpdateTable` operation on the existing `TableOperations` class, wire it into `DynamoDbRequestRouter`, and add integration tests in `MockDynamoDB.Tests.Spec`. The in-memory table definition will be mutated to reflect the new billing mode, provisioned throughput values, and GSI additions/updates/deletions.

## Scope
- Handle `X-Amz-Target: DynamoDB_20120810.UpdateTable`
- Accept and apply `BillingMode` changes (`PAY_PER_REQUEST` ↔ `PROVISIONED`)
- Accept and apply `ProvisionedThroughput` changes (ReadCapacityUnits, WriteCapacityUnits)
- Accept and apply `GlobalSecondaryIndexUpdates`:
  - `Create` — add a new GSI to the table definition
  - `Update` — change the provisioned throughput of an existing GSI
  - `Delete` — remove a GSI from the table definition
- Return a `TableDescription` reflecting the updated state (TableStatus remains `ACTIVE`)
- Return `ResourceNotFoundException` when the table does not exist
- Return `ValidationException` for invalid input (e.g., updating throughput on a PAY_PER_REQUEST table, duplicate GSI name, unknown GSI for Update/Delete)
- Integration tests covering each scenario above

## Out of Scope
- Stream specification changes (`StreamSpecification`)
- Table class changes (`TableClass`)
- Replica updates (`ReplicaUpdates`) — multi-region tables are not modelled
- Asynchronous `UPDATING` status — the mock returns `ACTIVE` immediately
- LSI changes (DynamoDB does not permit LSI modification after creation)
