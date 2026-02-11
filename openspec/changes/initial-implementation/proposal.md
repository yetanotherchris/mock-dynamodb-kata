# Initial Implementation Proposal

## Problem
Integration tests against DynamoDB require either DynamoDB Local (Java JAR), both of which are heavy dependencies. A lightweight, purpose-built mock in C#/.NET running in Docker would be simpler to use and faster to start.

## Solution
Build a mock DynamoDB server that implements the DynamoDB JSON wire protocol for the most commonly used operations. Not production-grade; designed purely for local integration testing.

## Scope
- Table operations (CreateTable, DeleteTable, DescribeTable, ListTables)
- Item CRUD (PutItem, GetItem, DeleteItem, UpdateItem)
- Expression engine (Condition, Filter, Projection, Update, KeyCondition expressions)
- Query and Scan (including parallel scan)
- Local Secondary Indexes
- Batch operations (BatchGetItem, BatchWriteItem)
- Transactions (TransactWriteItems, TransactGetItems)
- Docker support

## Out of Scope
- Global Secondary Indexes
- Streams / DynamoDB Streams
- Time to Live (TTL)
- On-demand / provisioned capacity simulation
- PartiQL support
- Backup/restore operations
- Global tables

Note - all the AWS models are available using Smithy, including DynamoDB. You might be able to use this, if there is a C# smithy reader (if not, perhaps another language could convert the smithy files)

https://github.com/aws/api-models-aws/blob/main/models/dynamodb/service/2012-08-10/dynamodb-2012-08-10.json