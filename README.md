# Mock DynamoDB (Kata)

In-memory mock DynamoDB server for local integration testing. 

## Usage

```bash
# Docker
docker build -t mock-dynamodb .
docker run -p 8000:8000 mock-dynamodb

# Direct
dotnet run --project src/MockDynamoDB.Server

# Custom port
MOCK_DYNAMODB_PORT=4566 dotnet run --project src/MockDynamoDB.Server
```

Configure the AWS SDK to point at `http://localhost:8000` with any credentials.

## Supported Operations

CreateTable, DeleteTable, DescribeTable, ListTables, PutItem, GetItem, DeleteItem, UpdateItem, Query, Scan, BatchGetItem, BatchWriteItem, TransactWriteItems, TransactGetItems

Local Secondary Indexes (LSI) and Global Secondary Indexes (GSI) are supported on Query.

## Supported Features

### Table Operations

| Feature | Supported |
|---|---|
| CreateTable | ✅ |
| DeleteTable | ✅ |
| DescribeTable | ✅ |
| ListTables (with pagination) | ✅ |
| UpdateTable | ❌ |
| TagResource / UntagResource / ListTagsOfResource | ❌ |

### Item Operations

| Feature | Supported |
|---|---|
| PutItem | ✅ |
| GetItem | ✅ |
| DeleteItem | ✅ |
| UpdateItem (SET, REMOVE, ADD, DELETE) | ✅ |
| ConditionExpression on writes | ✅ |
| ReturnValues (ALL_OLD, ALL_NEW, UPDATED_OLD, UPDATED_NEW) | ✅ |
| ConsistentRead (accepted, no-op) | ✅ |
| TTL / item expiry | ❌ |

### Query & Scan

| Feature | Supported |
|---|---|
| Query with KeyConditionExpression | ✅ |
| Query with FilterExpression | ✅ |
| Query ScanIndexForward (forward/reverse) | ✅ |
| Query with Limit and pagination | ✅ |
| Query with ProjectionExpression | ✅ |
| Query Select=COUNT | ✅ |
| Query ReturnConsumedCapacity | ✅ |
| Scan with FilterExpression | ✅ |
| Scan with Limit and pagination | ✅ |
| Scan with ProjectionExpression | ✅ |
| Parallel Scan (TotalSegments / Segment) | ✅ |

### Indexes

| Feature | Supported |
|---|---|
| Local Secondary Indexes (LSI) | ✅ |
| Global Secondary Indexes (GSI) | ✅ |
| GSI hash-key-only (no range key) | ✅ |
| GSI / LSI Projection: ALL | ✅ |
| GSI / LSI Projection: KEYS_ONLY | ❌ |
| GSI / LSI Projection: INCLUDE | ❌ |

### Expressions

| Feature | Supported |
|---|---|
| ExpressionAttributeNames (`#name`) | ✅ |
| ExpressionAttributeValues (`:val`) | ✅ |
| Comparison operators (`=`, `<>`, `<`, `<=`, `>`, `>=`) | ✅ |
| Logical operators (AND, OR, NOT) | ✅ |
| BETWEEN | ✅ |
| IN | ✅ |
| `attribute_exists` / `attribute_not_exists` | ✅ |
| `attribute_type` | ✅ |
| `begins_with` | ✅ |
| `contains` | ✅ |
| `size` | ✅ |
| `if_not_exists` (UpdateExpression) | ✅ |
| `list_append` (UpdateExpression) | ✅ |
| Document paths (nested maps, list indexes) | ✅ |
| PartiQL | ❌ |

### Batch & Transactions

| Feature | Supported |
|---|---|
| BatchGetItem (up to 100 keys) | ✅ |
| BatchWriteItem (up to 25 requests) | ✅ |
| TransactWriteItems (Put, Update, Delete, ConditionCheck) | ✅ |
| TransactGetItems | ✅ |
| TransactionCanceledException with CancellationReasons | ✅ |

### Other

| Feature | Supported |
|---|---|
| All attribute types (S, N, B, SS, NS, BS, BOOL, NULL, L, M) | ✅ |
| Streams / DynamoDB Streams | ❌ |
| DynamoDB Accelerator (DAX) | ❌ |
| Point-in-time recovery (PITR) | ❌ |
| Backup and restore | ❌ |
| Global Tables | ❌ |
| Auto Scaling | ❌ |

## Spec-Driven Development

This project uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) to manage requirements and track changes. Specifications in `openspec/specs/` define what the system should do. When a feature is planned, a change folder is created under `openspec/changes/` containing a proposal, design, and task checklist that reference the relevant specs. Once implemented and verified, changes are archived. This keeps requirements, design decisions, and implementation history in sync without heavyweight process.

## References

- [AWS API Models](https://github.com/aws/aws-models) - Smithy model definitions for AWS services including DynamoDB
- [Smithy](https://smithy.io/) - Interface definition language used by AWS
- [OpenSpec](https://github.com/Fission-AI/OpenSpec) - Spec-driven development framework for AI coding assistants
