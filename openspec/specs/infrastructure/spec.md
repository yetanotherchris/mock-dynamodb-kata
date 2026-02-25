# Infrastructure Specification

## Purpose

Define the repository layout, build configuration, and CI/CD infrastructure for the Mock DynamoDB solution.

---

## Project Structure

### Requirement: Solution File

The solution SHALL use the `.slnx` XML format (modern .NET solution file introduced in .NET 9).

#### Scenario: Solution contains all projects

- GIVEN the `.slnx` solution file exists at the repository root
- WHEN opened by an IDE or built with `dotnet build`
- THEN all `src/` and `tests/` projects are discoverable under organised virtual folders (`/src/`, `/tests/`)

### Requirement: Repository Layout

The repository SHALL use the following structure:

```
mock-dynamodb-kata/
├── .github/
│   └── workflows/
│       └── build-and-test.yml
├── openspec/
│   ├── constitution.md
│   ├── specs/
│   └── changes/
├── src/
│   ├── MockDynamoDB.Core/
│   │   └── MockDynamoDB.Core.csproj
│   └── MockDynamoDB.Server/
│       └── MockDynamoDB.Server.csproj
├── tests/
│   ├── MockDynamoDB.Tests.Unit/
│   │   └── MockDynamoDB.Tests.Unit.csproj
│   ├── MockDynamoDB.Tests.Spec/
│   │   └── MockDynamoDB.Tests.Spec.csproj
│   └── MockDynamoDB.Tests.Samples/
│       └── MockDynamoDB.Tests.Samples.csproj
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── Dockerfile
├── MockDynamoDB.slnx
└── CLAUDE.md
```

### Requirement: Centralised Build Configuration

`Directory.Build.props` at the repository root SHALL apply to all projects and include:

- `<LangVersion>latest</LangVersion>`
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

#### Scenario: Nullable warnings are errors

- GIVEN `TreatWarningsAsErrors` and `Nullable` are enabled
- WHEN code introduces a nullable dereference warning
- THEN the build fails

### Requirement: Central Package Management

`Directory.Packages.props` SHALL manage all NuGet package versions centrally with `ManagePackageVersionsCentrally=true`.

Individual project `.csproj` files SHALL omit version attributes from `<PackageReference>` elements.

### Requirement: SDK Pinning

`global.json` SHALL pin the .NET SDK version with `rollForward: "latestFeature"`.

---

## Docker and CI/CD

### Requirement: Multi-Stage Dockerfile

The `Dockerfile` at the repository root SHALL use a multi-stage build.

**Build stage:**

- Base image: `mcr.microsoft.com/dotnet/sdk:10.0`
- Restores and publishes `src/MockDynamoDB.Server` in Release configuration

**Runtime stage:**

- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Copies only the published output from the build stage
- Sets `ASPNETCORE_URLS=http://+:8000`
- Exposes port `8000`
- Entry point: `dotnet MockDynamoDB.Server.dll`

#### Scenario: Docker build produces a runnable image

- GIVEN the Dockerfile is present at the repository root
- WHEN `docker build -t mock-dynamodb .` is run
- THEN the image builds successfully without errors

#### Scenario: Server responds on port 8000

- GIVEN the image has been built
- WHEN `docker run -p 8000:8000 mock-dynamodb` is started
- THEN the server accepts HTTP requests on port 8000

### Requirement: GitHub Actions Workflow

`.github/workflows/build-and-test.yml` SHALL define a workflow triggered on:

- Push to `main`
- Pull requests targeting `main`

The workflow SHALL run on `ubuntu-latest` and contain the following steps in order:

1. Checkout the repository
2. Set up .NET 10 SDK
3. Restore dependencies (`dotnet restore`)
4. Build the solution (`dotnet build --no-restore -c Release`)
5. Run unit tests (`dotnet test tests/MockDynamoDB.Tests.Unit`)
6. Run integration tests (`dotnet test tests/MockDynamoDB.Tests.Spec`)
7. Pull the Moto image (`docker pull motoserver/moto:5.1.21`)
8. Start the Moto server (`docker run -d -p 5000:5000 motoserver/moto:5.1.21`) and wait for readiness
9. Run samples tests (`dotnet test tests/MockDynamoDB.Tests.Samples`)
10. Verify Dockerfile base image tags exist
11. Build and push the Docker image to `ghcr.io` — on push to `main` only

### Requirement: Docker Image Naming

The image SHALL be named `ghcr.io/${{ github.repository }}`.

It SHALL be tagged with:

- `latest` (on push to `main`)
- The full commit SHA

### Requirement: Registry Authentication

The workflow SHALL authenticate to `ghcr.io` using the `GITHUB_TOKEN` secret with `packages: write` permission.

#### Scenario: CI runs on pull request

- GIVEN a pull request is opened targeting `main`
- WHEN the CI workflow runs
- THEN build and test steps run
- THEN the Docker push step is skipped

#### Scenario: CI runs on push to main

- GIVEN a commit is pushed to `main`
- WHEN the CI workflow runs
- THEN all steps run including Docker build and push
- THEN the image is available at `ghcr.io/{owner}/{repo}:latest`
