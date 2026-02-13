# Design

## Architecture

The integration tests use ASP.NET Core's `WebApplicationFactory` to host the mock server in-process, eliminating the need for external processes or network ports:

```
Test Method → AmazonDynamoDBClient → HttpClient → TestHttpClientFactory
    → WebApplicationFactory.Server.CreateHandler() → In-process DynamoDbRequestRouter
```

## Test Fixture

A shared `MockDynamoDbFixture` implements `IAsyncLifetime` and provides a pre-configured `AmazonDynamoDBClient`:

- Creates a `WebApplicationFactory<Program>` on initialization
- Extracts the `HttpMessageHandler` from the test server
- Configures `AmazonDynamoDBConfig` with the test server's base address and `us-east-1` region
- Injects the handler via a custom `HttpClientFactory` subclass
- Uses `BasicAWSCredentials("fake", "fake")` since no real auth is needed
- Sets `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true` for SDK v4 compatibility

## Test Organization

Tests are organized by feature area using xUnit `IClassFixture<MockDynamoDbFixture>` for shared client lifecycle:

| Test Class | Spec Requirement | Table Setup |
|---|---|---|
| `TableOperationTests` | Table Operations via SDK | Per-test (creates/deletes own tables) |
| `ItemCrudTests` | Item CRUD via SDK | Per-class (hash+range key table via `IAsyncLifetime`) |
| `UpdateItemTests` | UpdateItem via SDK | Per-class (hash-only table via `IAsyncLifetime`) |
| `QueryScanTests` | Query + Scan Operations via SDK | Per-class (seeded with 6 items via `IAsyncLifetime`) |
| `LsiTests` | Local Secondary Index via SDK | Per-class (table with LSI, seeded with 4 items) |
| `BatchTransactionTests` | Batch + Transaction Operations via SDK | Per-class (hash-only table via `IAsyncLifetime`) |

## Table Isolation

Each test class uses `Guid.NewGuid()` in table names to prevent collisions when tests run in parallel against the shared fixture. Classes that need seeded data implement `IAsyncLifetime` with table creation in `InitializeAsync` and cleanup in `DisposeAsync`.

## SDK v4 Compatibility

AWS SDK v4 performs URI path canonicalization that can break requests to non-AWS endpoints. The fix is setting `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true` before creating the client. This is handled once in the fixture initialization.

## CI/CD

The GitHub Actions workflow (`build-and-test.yml`) runs on pull requests to main and pushes to main:
1. Restore dependencies
2. Build in Release configuration
3. Run unit tests (`MockDynamoDB.Tests.Unit`)
4. Run integration tests (`MockDynamoDB.Tests.Spec`)

No external services or AWS credentials are required since all tests use in-process hosting.
