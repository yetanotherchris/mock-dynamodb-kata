# Design

## Approach
Extend the existing `build-and-test.yml` workflow rather than creating a separate workflow. This ensures the Docker image is only published after all tests pass, and avoids duplicating the build/test steps.

## Workflow Changes
The existing `build-and-test` job will be extended with:

1. **Permissions**: Add `contents: read` and `packages: write` to the job
2. **Conditional publish**: Docker steps only run on pushes to main (not PRs or manual dispatch), using `if: github.event_name == 'push' && github.ref == 'refs/heads/main'`
3. **Docker Buildx**: `docker/setup-buildx-action@v3` for multi-platform builds
4. **GHCR Login**: `docker/login-action@v3` with `GITHUB_TOKEN`
5. **Build & Push**: `docker/build-push-action@v6` targeting the existing `Dockerfile`

## Image Tags
- `ghcr.io/<owner>/mock-dynamodb-kata:latest` — always points to the most recent main build
- `ghcr.io/<owner>/mock-dynamodb-kata:<short-sha>` — for traceability to specific commits

## Multi-Platform
Builds for `linux/amd64` and `linux/arm64` using Docker Buildx. The existing Dockerfile uses `mcr.microsoft.com/dotnet/aspnet:10.0-preview` which supports both architectures.

## Caching
Uses GitHub Actions cache (`type=gha`) for Docker layer caching to speed up builds where only application code has changed.
