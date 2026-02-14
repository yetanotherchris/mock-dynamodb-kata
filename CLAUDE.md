# CLAUDE.md

## Project Overview

Mock DynamoDB is an in-memory mock of AWS DynamoDB written in C#/.NET 10.0. It implements the DynamoDB JSON wire protocol for the 14 most commonly used operations, designed for local integration testing without requiring DynamoDB Local or AWS credentials.

## Tech Stack

- **.NET 10.0** (C#, nullable reference types, implicit usings)
- **ASP.NET Core** minimal API for HTTP hosting
- **ANTLR4** for expression parsing (condition, filter, key condition, update expressions)
- **xUnit v3** (3.2.2) for testing
- **AWS SDK for .NET v4** (`AWSSDK.DynamoDBv2` 4.0.14) in integration tests
- **Docker** multi-stage build, exposes port 8000

## Solution Structure

```
src/
  MockDynamoDB.Core/          # Business logic, no HTTP dependency
    Models/                   # AttributeValue, TableDefinition, DynamoDbError
    Storage/                  # ITableStore, IItemStore, InMemory implementations
    Operations/               # TableOperations, ItemOperations, QueryScanOperations,
                              # BatchOperations, TransactionOperations
    Expressions/              # ANTLR4 grammars, visitors, evaluators, AST, DocumentPath
      Grammar/                # DynamoDbCondition.g4, DynamoDbUpdate.g4
  MockDynamoDB.Server/        # ASP.NET host + HTTP middleware
    Middleware/               # DynamoDbRequestRouter (X-Amz-Target dispatch)
    Program.cs                # Entry point, DI registration
tests/
  MockDynamoDB.Tests.Unit/    # Expression parser unit tests (xUnit)
  MockDynamoDB.Tests.Spec/    # Integration tests using AWS SDK v4 (xUnit)
    Fixtures/                 # MockDynamoDbFixture (WebApplicationFactory-based)
```

## Build & Test Commands

```bash
# Restore and build
dotnet build

# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/MockDynamoDB.Tests.Unit

# Run integration/spec tests only
dotnet test tests/MockDynamoDB.Tests.Spec

# Run the server locally (default port 8000)
dotnet run --project src/MockDynamoDB.Server

# Run on a custom port
MOCK_DYNAMODB_PORT=4566 dotnet run --project src/MockDynamoDB.Server

# Docker
docker build -t mock-dynamodb .
docker run -p 8000:8000 mock-dynamodb
```

## Supported DynamoDB Operations

CreateTable, DeleteTable, DescribeTable, ListTables, PutItem, GetItem, DeleteItem, UpdateItem, Query, Scan, BatchGetItem, BatchWriteItem, TransactWriteItems, TransactGetItems

Local Secondary Indexes are supported on Query.

## Architecture Notes

- **Wire protocol**: POST to `/` with `X-Amz-Target: DynamoDB_20120810.{Operation}` header and `application/x-amz-json-1.0` content type
- **Storage**: `ConcurrentDictionary` for tables, per-table `SortedList` for items, separate sorted structures for LSIs
- **Concurrency**: `ReaderWriterLockSlim` — reader lock for normal operations, writer lock for transactions
- **Expression engine**: ANTLR4 grammars generate lexer/parser at build time; visitor classes produce an AST consumed by `ConditionEvaluator` and `UpdateEvaluator`
- **Numerics**: Uses C# `decimal` (28-29 significant digits vs DynamoDB's 38)
- **Health check**: `GET /` returns `{"status":"ok","service":"mock-dynamodb"}`

## Testing Conventions

- Integration tests use `WebApplicationFactory<Program>` for in-process hosting (no network)
- `MockDynamoDbFixture` provides a shared `AmazonDynamoDBClient` configured with fake credentials and `DisableDangerousDisablePathAndQueryCanonicalization = true`
- Test classes use `IClassFixture<MockDynamoDbFixture>` for shared client lifecycle
- Table names include `Guid.NewGuid()` for parallel test isolation
- Classes with seeded data implement `IAsyncLifetime` for setup/teardown

## OpenSpec Workflow

This project uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for spec-driven development. Requirements live in `openspec/specs/`, and proposed changes live in `openspec/changes/`.

### Directory Layout

```
openspec/
  specs/                          # Requirements (the "what")
    table-operations/spec.md
    item-crud/spec.md
    ...
  changes/                        # Change proposals (the "how")
    initial-implementation/       # One folder per logical unit of work
      proposal.md
      design.md
      tasks.md
    archive/                      # Completed changes (after /opsx:archive)
```

### Creating a Change

When implementing a new feature or modifying behaviour:

1. Create a change folder under `openspec/changes/<change-name>/`
2. **`proposal.md`** — Must include a `## Spec` section referencing the spec(s) it implements (e.g. `` `specs/aws-sdk-v4-integration` ``). Describes the problem, solution, scope, and out-of-scope.
3. **`design.md`** — Architecture decisions and technical approach.
4. **`tasks.md`** — Checklist of implementation tasks. Each task should reference its spec scenario where applicable (e.g. `— *Scenario: Create a hash-key table*`). Mark tasks `[x]` as they are completed.

### Key Conventions

- **Specs are requirements, changes are work.** Never edit a spec file directly when implementing — work through a change.
- **One change = one logical unit of work.** Don't combine unrelated features in a single change.
- **Reference the spec.** Every change proposal must have a `## Spec` section linking to the spec(s) it covers.
- **Trace tasks to scenarios.** Tasks in `tasks.md` should map back to specific spec scenarios for traceability.
- **Archive when done.** Completed changes move to `openspec/changes/archive/` after verification.

## Not Supported

Global Secondary Indexes, DynamoDB Streams, TTL, provisioned capacity simulation, PartiQL, backup/restore, global tables.
