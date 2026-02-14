# AWS SDK v4 Integration Tests

## Spec
`specs/aws-sdk-v4-integration`

## Problem
The mock DynamoDB server has comprehensive unit tests for its expression engine but lacks end-to-end integration tests that use the real AWS SDK for .NET v4 (`AWSSDK.DynamoDBv2` v4.x). Without these tests, there is no verification that the server's wire protocol implementation is compatible with the actual SDK client, including its JSON serialization, header conventions, and URI canonicalization behaviour.

## Solution
Add an integration test suite that exercises every supported DynamoDB operation through a real `AmazonDynamoDBClient` connected to the mock server via ASP.NET Core's `WebApplicationFactory` in-process hosting. This validates end-to-end compatibility without requiring network access or AWS credentials.

## Scope
- Test fixture using `WebApplicationFactory<Program>` with a custom `HttpClientFactory` handler
- AWS SDK v4 path canonicalization workaround (`DisableDangerousDisablePathAndQueryCanonicalization`)
- Integration tests for all 14 supported DynamoDB operations:
  - Table operations: CreateTable, DeleteTable, DescribeTable, ListTables
  - Item CRUD: PutItem, GetItem, DeleteItem (including all attribute types)
  - UpdateItem: SET, REMOVE, ADD, DELETE, arithmetic, conditions, upsert, ReturnValues
  - Query: partition key, sort key conditions (begins_with, BETWEEN), reverse order, filter, limit
  - Scan: full scan, filter, limit, pagination
  - Batch: BatchWriteItem, BatchGetItem
  - Transactions: TransactWriteItems (success and condition failure), TransactGetItems
  - LSI: query by index, sort key conditions, reverse order, projected attributes, non-existent index
- CI/CD pipeline enabled for pull requests and main branch pushes
- Error handling verification (ResourceNotFoundException, ResourceInUseException, ConditionalCheckFailedException, TransactionCanceledException)

## Out of Scope
- Performance or load testing
- AWS Signature V4 signature validation (fake credentials are used)
- Testing against real DynamoDB or DynamoDB Local
- GSI integration tests (GSIs are not implemented)
