# Docker GitHub Registry Specification

## Purpose
The project SHALL publish a Docker image to the GitHub Container Registry (ghcr.io) as part of the CI build, making the mock DynamoDB server available for consumption without building from source.

## Requirements

### Requirement: Publish on Main Build
The CI pipeline SHALL build and push a Docker image to ghcr.io on every push to the `main` branch.

#### Scenario: Push to main triggers image publish
- **WHEN** a commit is pushed to the `main` branch
- **AND** the build and tests pass
- **THEN** a Docker image SHALL be built and pushed to `ghcr.io/<owner>/mock-dynamodb-kata`

#### Scenario: Pull request does not publish
- **WHEN** a pull request is opened or updated against `main`
- **THEN** the Docker image SHALL NOT be pushed to the registry

### Requirement: Image Tagging
The published image SHALL be tagged with both `latest` and the short Git SHA for traceability.

#### Scenario: Image tags on main push
- **GIVEN** a push to `main` with commit SHA `abc1234def`
- **WHEN** the Docker image is published
- **THEN** the image SHALL be tagged as `latest`
- **AND** the image SHALL be tagged with the short SHA (e.g. `abc1234`)

### Requirement: Multi-Platform Support
The published image SHALL support both `linux/amd64` and `linux/arm64` platforms.

#### Scenario: Multi-arch build
- **WHEN** the Docker image is built
- **THEN** it SHALL produce manifests for `linux/amd64` and `linux/arm64`

### Requirement: Image Metadata
The published image SHALL include OCI labels for title, description, source URL, and revision.

#### Scenario: OCI labels present
- **WHEN** the Docker image is published
- **THEN** it SHALL include `org.opencontainers.image.title` set to `MockDynamoDB`
- **AND** `org.opencontainers.image.description` describing the project
- **AND** `org.opencontainers.image.source` linking to the GitHub repository
- **AND** `org.opencontainers.image.revision` set to the full commit SHA

### Requirement: Authentication
The workflow SHALL authenticate to ghcr.io using the built-in `GITHUB_TOKEN` with `packages: write` permission.

#### Scenario: GHCR authentication
- **GIVEN** the GitHub Actions job has `permissions.packages: write`
- **WHEN** the Docker login step runs
- **THEN** it SHALL authenticate to `ghcr.io` using `github.actor` and `secrets.GITHUB_TOKEN`

### Requirement: Build Caching
The workflow SHALL use GitHub Actions cache for Docker layer caching to speed up subsequent builds.

#### Scenario: Layer caching
- **WHEN** the Docker image is built
- **THEN** it SHALL use `cache-from: type=gha` and `cache-to: type=gha,mode=max`
