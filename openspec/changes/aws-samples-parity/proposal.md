# AWS Samples Parity Tests

## Spec
`specs/item-crud`, `specs/query`, `specs/scan`, `specs/table-operations`, `specs/batch-operations`, `specs/transactions`, `specs/update-item`

## Problem
The existing integration test suite validates the mock DynamoDB implementation through hand-crafted test cases. There is no verification that the mock faithfully reproduces the same behaviour as a real DynamoDB-compatible server when the exact patterns from official AWS samples are used. Additionally, there is no mechanism to run the same test suite against a second implementation (moto) to confirm parity.

## Solution
Add a new test project (`MockDynamoDB.Tests.Samples`) that:

1. **Copies the example patterns** from the [aws-samples/aws-dynamodb-examples dotnet sdk_v2](https://github.com/aws-samples/aws-dynamodb-examples/tree/master/examples/SDK/dotnet/sdk_v2) source as test cases, adapting them for TUnit's async assertion style while preserving the original table schemas, attribute names, and expression patterns.
2. **Runs against two backends** using a parameterised `[ClassDataSource]` approach:
   - **MockDynamoDB** — in-process via `WebApplicationFactory<Program>`, identical to the existing spec test fixture.
   - **Moto** — the `motoserver/moto:5.1.21` Docker image, started and stopped via Testcontainers.DotNet once per test session.

Tests that pass against both backends confirm parity; any divergence surfaces a gap in the mock implementation.

## Scope
- New test project `tests/MockDynamoDB.Tests.Samples`
- `IMockBackend` abstraction (provides `AmazonDynamoDBClient`) with two implementations:
  - `MockDynamoDbBackend` — wraps `WebApplicationFactory<Program>`
  - `MotoBackend` — wraps a Testcontainers `motoserver/moto:5.1.21` container
- Test classes parameterised with `[ClassDataSource<IMockBackend>(Shared = SharedType.PerTestSession)]`:
  - `WorkingWithTablesTests` — based on `WorkingWithTables/CreateTableProvisioned.cs`
  - `WorkingWithItemsTests` — based on `WorkingWithItems/{PutItem, GetItem, UpdateItem, UpdateItemConditional, DeleteItem, DeleteItemConditional, PutItemConditional}.cs`
  - `BatchItemsTests` — based on `WorkingWithItems/{BatchGetItem, BatchWriteItem}.cs`
  - `TransactItemsTests` — based on `WorkingWithItems/{TransactGetItems, TransactWriteItems}.cs`
  - `WorkingWithQueriesTests` — based on `WorkingWithQueries/{QueryFilterExpression, QueryProjectionExpression, QueryCount, QueryConsistentRead, QueryConsumedCapacity}.cs`
- Table names suffixed with `Guid.NewGuid()` for parallel isolation
- Moto container image pinned to `motoserver/moto:5.1.21`

## Out of Scope
- Modifying the existing `MockDynamoDB.Tests.Spec` project
- Adding new operations to the mock that are not already supported
- Tests for unsupported operations (GSI, Streams, TTL, PartiQL)
- Performance or load testing
- AWS Signature V4 validation
