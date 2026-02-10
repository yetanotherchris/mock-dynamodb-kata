# Tasks

## Phase 1: Skeleton + Table Operations
- [x] Create solution and project structure
- [ ] HTTP routing middleware (POST /, X-Amz-Target dispatch)
- [ ] CreateTable, DeleteTable, DescribeTable, ListTables
- [ ] In-memory table store

## Phase 2: Item CRUD
- [ ] AttributeValue model
- [ ] DynamoDbJsonSerializer
- [ ] InMemoryItemStore
- [ ] PutItem, GetItem, DeleteItem handlers

## Phase 3: Expression Engine
- [ ] Tokenizer
- [ ] Document path parser
- [ ] ExpressionAttributeNames/Values resolution
- [ ] ProjectionExpression

## Phase 4: ConditionExpression + FilterExpression
- [ ] Condition parser (AST)
- [ ] Comparison operators and functions
- [ ] Condition evaluator
- [ ] Wire into PutItem, DeleteItem

## Phase 5: UpdateExpression + UpdateItem
- [ ] Update expression parser (SET, REMOVE, ADD, DELETE)
- [ ] SET functions (if_not_exists, list_append, arithmetic)
- [ ] ReturnValues support
- [ ] UpdateItem handler

## Phase 6: Query
- [ ] KeyConditionExpression parser
- [ ] Query handler with pagination
- [ ] ScanIndexForward
- [ ] FilterExpression + ProjectionExpression on query

## Phase 7: Scan
- [ ] Scan handler with pagination
- [ ] Parallel scan (FNV-1a segment assignment)
- [ ] FilterExpression + ProjectionExpression on scan

## Phase 8: LSI
- [ ] LSI validation on CreateTable
- [ ] Per-LSI index maintenance
- [ ] Query with IndexName

## Phase 9: Batch Operations
- [ ] BatchGetItem
- [ ] BatchWriteItem

## Phase 10: Transactions
- [ ] TransactWriteItems
- [ ] TransactGetItems
- [ ] ReaderWriterLockSlim isolation

## Phase 11: Docker + Polish
- [ ] Multi-stage Dockerfile
- [ ] Health check endpoint
- [ ] Review and edge case fixes
