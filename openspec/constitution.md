# Project Constitution

Non-negotiable rules for this project. These must not be changed without explicit agreement.

## Runtime and Language

- Target: .NET 10, C# (LangVersion: latest)
- Server: ASP.NET Core minimal API
- All data stored in memory only — no disk writes, no persistence across restarts

## Project Structure

- Solution file: `MockDynamoDB.slnx` (modern XML format)
- Source: `src/MockDynamoDB.Core/`, `src/MockDynamoDB.Server/`
- Tests: `tests/MockDynamoDB.Tests.Unit/`, `tests/MockDynamoDB.Tests.Spec/`, `tests/MockDynamoDB.Tests.Samples/`
- Centralised build config: `Directory.Build.props` at root
- Centralised package versions: `Directory.Packages.props` at root

## Build Requirements

After every change:

1. `dotnet build MockDynamoDB.slnx` must succeed with **0 errors and 0 warnings**
2. `dotnet test` must pass with **0 failures**

`TreatWarningsAsErrors` is enabled globally and must remain so.

## Package Management

- All NuGet versions are managed centrally in `Directory.Packages.props`
- `ManagePackageVersionsCentrally=true` must remain set
- No version attributes in individual `.csproj` files

## Testing

- Test framework: TUnit only
- `MockDynamoDB.Tests.Unit`: pure unit tests (expression parser, no HTTP, no network)
- `MockDynamoDB.Tests.Spec`: AWS SDK v4 integration tests via `WebApplicationFactory` (in-process)
- `MockDynamoDB.Tests.Samples`: AWS SDK example pattern tests — abstract base + `MockDynamoDB_` and `Moto_` concrete subclasses; Moto tests skipped when server is not running

## DynamoDB API

- Wire protocol: POST to `/` with `X-Amz-Target: DynamoDB_20120810.{Operation}` header
- Content type: `application/x-amz-json-1.0`
- Authentication: bypass — any credentials accepted, no signature validation
- Port 8000 by default, configurable via `MOCK_DYNAMODB_PORT` environment variable

## Docker

- Multi-stage Dockerfile: SDK image for build, ASP.NET runtime image for output
- `ASPNETCORE_URLS=http://+:8000`
- Exposes port 8000
- Published to `ghcr.io` on push to `main` only

## Commit and Branch Discipline

- Always create a branch before starting work
- Commit as you go — small, focused commits after each logical unit of work
- Do not commit if build or tests fail
- Create a PR when finished using `gh pr create` with `--body-file`
