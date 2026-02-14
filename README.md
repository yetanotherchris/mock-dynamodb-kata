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

Local Secondary Indexes are supported on Query.

## Spec-Driven Development

This project uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) to manage requirements and track changes. Specifications in `openspec/specs/` define what the system should do. When a feature is planned, a change folder is created under `openspec/changes/` containing a proposal, design, and task checklist that reference the relevant specs. Once implemented and verified, changes are archived. This keeps requirements, design decisions, and implementation history in sync without heavyweight process.

## References

- [AWS API Models](https://github.com/aws/aws-models) - Smithy model definitions for AWS services including DynamoDB
- [Smithy](https://smithy.io/) - Interface definition language used by AWS
- [OpenSpec](https://github.com/Fission-AI/OpenSpec) - Spec-driven development framework for AI coding assistants
