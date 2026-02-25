# Design

## Architecture

Two backends are abstracted behind `IMockBackend` so that every test class can be
driven by either in-process MockDynamoDB or a Docker-hosted Moto container without
changing the test logic:

```
Test Method → AmazonDynamoDBClient
                  ├─ MockDynamoDbBackend → WebApplicationFactory<Program> (in-process)
                  └─ MotoBackend → Testcontainers → motoserver/moto:5.1.21 (Docker)
```

## Backend Abstraction

```csharp
public interface IMockBackend : IAsyncInitializer, IAsyncDisposable
{
    AmazonDynamoDBClient Client { get; }
}
```

### `MockDynamoDbBackend`

Identical in construction to the existing `MockDynamoDbFixture`:

- Creates `WebApplicationFactory<Program>` and extracts its `HttpMessageHandler`
- Builds `AmazonDynamoDBClient` with `BasicAWSCredentials("fake","fake")` and the test handler
- Sets `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true`

### `MotoBackend`

- Uses `Testcontainers.DotNet` (`DotNet.Testcontainers` NuGet package) to start
  `motoserver/moto:5.1.21` on an ephemeral port
- Waits for the container's HTTP health endpoint to become responsive
- Builds `AmazonDynamoDBClient` pointed at `http://localhost:<port>` with fake credentials

## Test Parameterisation

TUnit's `[ClassDataSource]` accepts a type argument that implements `IAsyncInitializer`.
Each test class declares:

```csharp
[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
public class WorkingWithItemsTests(IMockBackend backend)
```

This causes TUnit to instantiate and run the full class twice — once per backend —
during a single `dotnet test` invocation.

## Test Structure

Each test class mirrors the folder structure from the aws-samples repository:

| Test Class | Source Examples | Table Schema |
|---|---|---|
| `WorkingWithTablesTests` | `CreateTableProvisioned.cs` | Hash + Range (`PK` S, `SK` S) — `MyTable` |
| `WorkingWithItemsTests` | `PutItem`, `GetItem`, `UpdateItem`, `UpdateItemConditional`, `DeleteItem`, `DeleteItemConditional`, `PutItemConditional` | Hash + Range (`pk` S, `sk` S) — `RetailDatabase` |
| `BatchItemsTests` | `BatchGetItem`, `BatchWriteItem` | Hash + Range (`pk` S, `sk` S) |
| `TransactItemsTests` | `TransactGetItems`, `TransactWriteItems` | Hash + Range (`pk` S, `sk` S) |
| `WorkingWithQueriesTests` | `QueryFilterExpression`, `QueryProjectionExpression`, `QueryCount`, `QueryConsistentRead`, `QueryConsumedCapacity` | Hash + Range (`PK` S, `SK` S) with `CustomerName` S attribute |

## Data Fidelity

Test cases preserve the original example data verbatim:

- `RetailDatabase` table uses `pk = "jim.bob@somewhere.com"`, `sk = "metadata"`, and the full
  address map attribute from `PutItem.cs`
- `MyTable` uses `PK = "Customer1"` entries as in the query examples
- Where examples only print success/failure, tests add `Assert.That` checks on the response
  status or returned attributes to make the test meaningful

## Moto Parity Strategy

- Tests run against MockDynamoDB first (fast, in-process)
- If a test fails on Moto but passes on MockDynamoDB, a known-gap comment is added and
  the Moto run is skipped with `[Skip]` until the gap is resolved
- The reverse (passes Moto, fails MockDynamoDB) indicates a mock implementation gap to fix

## Project Configuration

`tests/MockDynamoDB.Tests.Samples/MockDynamoDB.Tests.Samples.csproj`:

```xml
<PackageReference Include="AWSSDK.DynamoDBv2" Version="4.0.14" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
<PackageReference Include="Testcontainers" Version="3.*" />
<PackageReference Include="TUnit" Version="*" />
```

The project references `MockDynamoDB.Server` for `WebApplicationFactory<Program>`.
