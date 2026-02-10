# Design

## Architecture
- **MockDynamoDB.Server**: ASP.NET minimal API, single POST endpoint, routes via X-Amz-Target header
- **MockDynamoDB.Core**: Business logic, storage, expression engine - no HTTP dependency
- **Reference**: Smithy model from aws/api-models-aws used for wire-protocol accuracy

## Storage
- In-memory ConcurrentDictionary for tables
- Per-table: ConcurrentDictionary<partitionKey, SortedList<sortKey, Item>> for items
- LSI: separate sorted structures maintained on writes
- No persistence; data lost on restart

## Expression Engine
- Hand-written recursive descent tokenizer and parser
- AST nodes for conditions, updates, projections, key conditions
- Evaluator walks AST against items

## Concurrency
- ReaderWriterLockSlim: reader lock for normal operations, writer lock for transactions
- ConcurrentDictionary for table-level operations

## Wire Protocol
- Content-Type: application/x-amz-json-1.0
- X-Amz-Target: DynamoDB_20120810.{OperationName}
- Error format: {"__type": "fully.qualified#ErrorType", "Message": "..."}

## Numerics
- C# decimal type (28-29 significant digits vs DynamoDB's 38)
- Acceptable trade-off for testing purposes
