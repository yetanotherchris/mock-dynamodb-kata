# Testing Specification

## Purpose

Define the test projects, fixtures, and compatibility requirements for testing the Mock DynamoDB server.

---

## AWS SDK Integration Tests

### Requirement: Project Setup

`tests/MockDynamoDB.Tests.Spec` SHALL be a TUnit test project targeting `net10.0`.

It SHALL reference:

- `MockDynamoDB.Core` and `MockDynamoDB.Server` (project references)
- `AWSSDK.DynamoDBv2` NuGet package
- `Microsoft.AspNetCore.Mvc.Testing` NuGet package
- TUnit test framework packages

### Requirement: In-Process Test Hosting

The integration tests SHALL run the mock DynamoDB server in-process using ASP.NET Core's `WebApplicationFactory<Program>` so that no external process or network port is required.

#### Scenario: Server starts via WebApplicationFactory

- WHEN a test fixture creates a `WebApplicationFactory<Program>`
- THEN the server SHALL start successfully in-process
- AND expose an `HttpMessageHandler` via `factory.Server.CreateHandler()`

#### Scenario: SDK client connects through test handler

- GIVEN a `WebApplicationFactory<Program>` is running
- WHEN an `AmazonDynamoDBClient` is configured with:
  - `ServiceURL` set to the test server's base address
  - `AuthenticationRegion` set to `us-east-1`
  - A custom `HttpClientFactory` that injects the test server's handler
  - Fake `BasicAWSCredentials`
- THEN the SDK client SHALL communicate with the in-process server
- AND all DynamoDB operations SHALL function without network calls

### Requirement: AWS SDK v4 Compatibility

The server SHALL handle requests from `AWSSDK.DynamoDBv2` v4.x, including its URI canonicalization behaviour.

#### Scenario: Path canonicalization disabled

- GIVEN `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization` is set to `true`
- WHEN the SDK sends requests to the mock server
- THEN the server SHALL process requests without signature validation errors

### Requirement: MockDynamoDbFixture

A shared `MockDynamoDbFixture` class SHALL manage the lifecycle of the in-process test host and the configured SDK client.

It SHALL:

- Implement `IAsyncInitializer` and `IAsyncDisposable`
- Create a `WebApplicationFactory<Program>` instance
- Create an `AmazonDynamoDBClient` configured with fake credentials, `ServiceURL` pointing to the in-process server, and a per-client `HttpClientFactory` injecting the test handler
- Be shared across all tests in the session using `[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]`

### Requirement: Per-Test Table Isolation

Each test class SHALL operate on uniquely named tables (using `Guid.NewGuid()`) to avoid cross-test interference when tests run in parallel.

#### Scenario: Isolated test execution

- GIVEN two tests run concurrently
- WHEN each test creates items in its own table
- THEN neither test observes the other's items

### Requirement: Test Coverage

The spec test project SHALL include tests for all supported operations: table lifecycle, item CRUD, UpdateItem, Query, Scan, BatchGetItem, BatchWriteItem, TransactWriteItems, TransactGetItems, and LSI queries.

---

## AWS Samples Parity Tests

### Requirement: Purpose

To verify that Mock DynamoDB correctly implements the patterns shown in AWS SDK documentation and examples, the same test scenarios SHALL run against both MockDynamoDB (in-process) and Moto (external server).

### Requirement: Project Setup

`tests/MockDynamoDB.Tests.Samples` SHALL be a TUnit test project targeting `net10.0`.

It SHALL reference:

- `MockDynamoDB.Core` and `MockDynamoDB.Server` (project references)
- `AWSSDK.DynamoDBv2` NuGet package
- `Microsoft.AspNetCore.Mvc.Testing` NuGet package
- TUnit test framework packages

### Requirement: Backend Abstraction

An `IMockBackend` interface SHALL expose:

- `AmazonDynamoDBClient Client` — the configured SDK client
- `bool IsAvailable` — whether the backend is ready to accept requests

### Requirement: MockDynamoDbBackend

`MockDynamoDbBackend` SHALL start the server in-process via `WebApplicationFactory<Program>`.

- `IsAvailable` SHALL always be `true`
- SHALL use a per-client `HttpClientFactory` (NOT the global `AWSConfigs.HttpClientFactory`) to avoid routing conflicts when multiple backends exist in the same process

### Requirement: MotoBackend

`MotoBackend` SHALL connect to an already-running Moto server.

- Default port: `5000`; overridable via `MOTO_PORT` environment variable
- `InitializeAsync` SHALL probe `http://127.0.0.1:{port}/` with a 2-second timeout
- If the server responds, `IsAvailable` is `true` and the client is configured
- If the server is not reachable, `IsAvailable` is `false` and no exception is thrown
- No Docker orchestration in test code — the server is assumed to be started externally

#### Scenario: Moto not running

- GIVEN no process is listening on port 5000
- WHEN `MotoBackend.InitializeAsync()` runs
- THEN `IsAvailable` is `false`
- THEN no exception is thrown

#### Scenario: Moto running

- GIVEN `motoserver/moto:5.1.21` is running on port 5000
- WHEN `MotoBackend.InitializeAsync()` runs
- THEN `IsAvailable` is `true`
- THEN `Client` is configured to point at `http://127.0.0.1:5000`

### Requirement: Test Class Structure

Each test file SHALL contain:

- An `abstract` base class with all `[Test]` methods
- A `MockDynamoDB_` concrete subclass decorated with `[ClassDataSource<MockDynamoDbBackend>(...)]` and `[InheritsTests]`
- A `Moto_` concrete subclass decorated with `[ClassDataSource<MotoBackend>(...)]` and `[InheritsTests]`

#### Scenario: Moto tests skipped when unavailable

- GIVEN `MotoBackend.IsAvailable` is `false`
- WHEN any `Moto_` test runs
- THEN `Skip.Test(...)` is called in `[Before(Test)]`
- THEN the test is marked skipped, not failed

### Requirement: Test Coverage

The samples test project SHALL include tests covering patterns from AWS documentation:

- **WorkingWithTables**: CreateTable (provisioned), duplicate name throws, DeleteTable
- **WorkingWithItems**: PutItem with nested map, GetItem, UpdateItem with ExpressionAttributeNames, conditional UpdateItem, conditional DeleteItem, conditional PutItem
- **BatchItems**: BatchWriteItem, BatchGetItem, absent keys not returned
- **TransactItems**: TransactWriteItems atomic success, TransactWriteItems rollback on condition failure, TransactGetItems
- **WorkingWithQueries**: FilterExpression, ProjectionExpression, Select COUNT, ConsistentRead, ConsumedCapacity

### Requirement: Test Skip When Moto Absent

If `MotoBackend.IsAvailable` is `false`, all `Moto_` tests SHALL be skipped with a message instructing how to start the server:

```
docker run -d -p 5000:5000 motoserver/moto:5.1.21
```
