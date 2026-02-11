# Tasks

## Phase 1: Skeleton + Table Operations
- [x] Create solution and project structure
- [x] HTTP routing middleware (POST /, X-Amz-Target dispatch)
- [x] CreateTable, DeleteTable, DescribeTable, ListTables
- [x] In-memory table store

## Phase 2: Item CRUD
- [x] AttributeValue model
- [x] DynamoDbJsonSerializer
- [x] InMemoryItemStore
- [x] PutItem, GetItem, DeleteItem handlers

## Phase 3: Expression Engine
- [x] Tokenizer
- [x] Document path parser
- [x] ExpressionAttributeNames/Values resolution
- [x] ProjectionExpression

## Phase 4: ConditionExpression + FilterExpression
- [x] Condition parser (AST)
- [x] Comparison operators and functions
- [x] Condition evaluator
- [x] Wire into PutItem, DeleteItem

## Phase 5: UpdateExpression + UpdateItem
- [x] Update expression parser (SET, REMOVE, ADD, DELETE)
- [x] SET functions (if_not_exists, list_append, arithmetic)
- [x] ReturnValues support
- [x] UpdateItem handler

## Phase 6: Query
- [x] KeyConditionExpression parser
- [x] Query handler with pagination
- [x] ScanIndexForward
- [x] FilterExpression + ProjectionExpression on query

## Phase 7: Scan
- [x] Scan handler with pagination
- [x] Parallel scan (FNV-1a segment assignment)
- [x] FilterExpression + ProjectionExpression on scan

## Phase 8: LSI
- [x] LSI validation on CreateTable
- [x] Per-LSI index maintenance
- [x] Query with IndexName

## Phase 9: Batch Operations
- [x] BatchGetItem
- [x] BatchWriteItem

## Phase 10: Transactions
- [x] TransactWriteItems
- [x] TransactGetItems
- [x] ReaderWriterLockSlim isolation

## Phase 11: Docker + Polish
- [x] Multi-stage Dockerfile
- [x] Health check endpoint
- [x] Review and edge case fixes
