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

## References

- [AWS API Models](https://github.com/aws/aws-models) - Smithy model definitions for AWS services including DynamoDB
- [Smithy](https://smithy.io/) - Interface definition language used by AWS
