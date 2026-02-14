# Docker GitHub Registry Proposal

## Spec
`specs/docker-github-registry`

## Problem
The Docker image for mock-dynamodb must currently be built from source. There is no published image available, which makes it harder for consumers to use the project in their own CI pipelines or local development without cloning the repository.

## Solution
Add Docker image build-and-push steps to the existing `build-and-test` GitHub Actions workflow. On pushes to `main`, after the build and tests pass, the workflow will build a multi-platform Docker image and push it to the GitHub Container Registry (`ghcr.io`).

This follows the same pattern used in the `letmein` repository's `docker-publish.yml` workflow, adapted to:
- Trigger on main branch pushes (not tags) since mock-dynamodb-kata uses continuous deployment from main
- Tag images with `latest` and the short Git SHA (rather than semver, since there is no tag-based release process yet)
- Be integrated into the existing `build-and-test.yml` workflow rather than a separate workflow file

## Scope
- Add `packages: write` permission to the existing workflow job
- Add Docker Buildx setup, GHCR login, and build-and-push steps after the test steps
- Multi-platform build (`linux/amd64`, `linux/arm64`)
- OCI image labels for metadata
- GitHub Actions layer caching

## Out of Scope
- Tag-based versioning / semver image tags (can be added later)
- Separate Docker publish workflow file
- Publishing to Docker Hub or other registries
