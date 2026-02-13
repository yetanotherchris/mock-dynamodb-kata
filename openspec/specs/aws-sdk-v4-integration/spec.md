# AWS SDK v4 Integration Tests Specification

## Purpose
The mock DynamoDB server SHALL be verified through integration tests that use the official AWS SDK for .NET v4 (`AWSSDK.DynamoDBv2` v4.x) against an in-process server hosted via `WebApplicationFactory`. These tests validate that the server correctly implements the DynamoDB wire protocol as consumed by a real AWS SDK client.

## Requirements

### Requirement: In-Process Test Hosting
The integration tests SHALL run the mock DynamoDB server in-process using ASP.NET Core's `WebApplicationFactory<Program>` so that no external process or network port is required.

#### Scenario: Server starts via WebApplicationFactory
- **WHEN** a test fixture creates a `WebApplicationFactory<Program>`
- **THEN** the server SHALL start successfully in-process
- **AND** expose an `HttpMessageHandler` via `factory.Server.CreateHandler()`

#### Scenario: SDK client connects through test handler
- **GIVEN** a `WebApplicationFactory<Program>` is running
- **WHEN** an `AmazonDynamoDBClient` is configured with:
  - `ServiceURL` set to the test server's base address
  - `AuthenticationRegion` set to `us-east-1`
  - A custom `HttpClientFactory` that injects the test server's handler
  - Fake `BasicAWSCredentials`
- **THEN** the SDK client SHALL communicate with the in-process server
- **AND** all DynamoDB operations SHALL function without network calls

### Requirement: AWS SDK v4 Compatibility
The server SHALL handle requests from `AWSSDK.DynamoDBv2` v4.x, including its URI canonicalization behaviour.

#### Scenario: Path canonicalization disabled
- **GIVEN** `AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization` is set to `true`
- **WHEN** the SDK sends requests to the mock server
- **THEN** the server SHALL process requests without signature validation errors

### Requirement: Table Operations via SDK
The server SHALL support table lifecycle operations when invoked through the AWS SDK v4 client.

#### Scenario: Create a hash-key table
- **WHEN** `CreateTableAsync` is called with a hash key schema
- **THEN** the response SHALL contain `TableStatus` of `ACTIVE`
- **AND** `DescribeTableAsync` SHALL return the table definition

#### Scenario: Create a composite-key table
- **WHEN** `CreateTableAsync` is called with hash and range key schema
- **THEN** the response SHALL contain both key elements
- **AND** `TableStatus` SHALL be `ACTIVE`

#### Scenario: Create duplicate table
- **GIVEN** a table with the specified name already exists
- **WHEN** `CreateTableAsync` is called with the same name
- **THEN** the SDK SHALL throw `ResourceInUseException`

#### Scenario: Delete a table
- **GIVEN** a table exists
- **WHEN** `DeleteTableAsync` is called
- **THEN** the table SHALL be removed
- **AND** subsequent `DescribeTableAsync` SHALL throw `ResourceNotFoundException`

#### Scenario: List tables
- **GIVEN** multiple tables exist
- **WHEN** `ListTablesAsync` is called
- **THEN** the response SHALL contain all table names

### Requirement: Item CRUD via SDK
The server SHALL support item-level operations when invoked through the AWS SDK v4 client.

#### Scenario: Put and get an item
- **GIVEN** a table exists with hash and range keys
- **WHEN** `PutItemAsync` is called with key and non-key attributes
- **AND** `GetItemAsync` is called with the same key
- **THEN** the response SHALL contain the stored item with all attributes

#### Scenario: Get non-existent item
- **WHEN** `GetItemAsync` is called with a key that does not exist
- **THEN** `IsItemSet` SHALL be `false`

#### Scenario: Replace existing item
- **GIVEN** an item exists with a given key
- **WHEN** `PutItemAsync` is called with the same key but different attributes
- **AND** `GetItemAsync` is called
- **THEN** the response SHALL contain the updated attributes

#### Scenario: Delete an item
- **GIVEN** an item exists
- **WHEN** `DeleteItemAsync` is called with the item's key
- **THEN** subsequent `GetItemAsync` SHALL return `IsItemSet` as `false`

#### Scenario: Delete with return values
- **GIVEN** an item exists
- **WHEN** `DeleteItemAsync` is called with `ReturnValues = ALL_OLD`
- **THEN** the response SHALL contain the deleted item's attributes

#### Scenario: All attribute types
- **WHEN** `PutItemAsync` is called with attributes of types S, N, BOOL, NULL, L, M, SS, NS
- **AND** `GetItemAsync` retrieves the item
- **THEN** each attribute SHALL be returned with the correct type and value

#### Scenario: Operation on non-existent table
- **WHEN** `PutItemAsync` is called with a table name that does not exist
- **THEN** the SDK SHALL throw `ResourceNotFoundException`

### Requirement: Query Operations via SDK
The server SHALL support query operations with key conditions and filters when invoked through the AWS SDK v4 client.

#### Scenario: Query by partition key
- **GIVEN** a table with items under partition key `user1`
- **WHEN** `QueryAsync` is called with `KeyConditionExpression = "pk = :pk"`
- **THEN** the response SHALL contain all items for that partition

#### Scenario: Query with begins_with on sort key
- **WHEN** `QueryAsync` is called with `begins_with(sk, :prefix)` in the key condition
- **THEN** the response SHALL contain only items whose sort key starts with the prefix

#### Scenario: Query with BETWEEN on sort key
- **WHEN** `QueryAsync` is called with `sk BETWEEN :low AND :high`
- **THEN** the response SHALL contain only items with sort keys in the specified range

#### Scenario: Query in reverse order
- **WHEN** `QueryAsync` is called with `ScanIndexForward = false`
- **THEN** items SHALL be returned in descending sort key order

#### Scenario: Query with filter expression
- **WHEN** `QueryAsync` is called with a `FilterExpression`
- **THEN** the response SHALL contain only items matching both key condition and filter

#### Scenario: Query with limit
- **WHEN** `QueryAsync` is called with a `Limit`
- **THEN** at most `Limit` items SHALL be returned
- **AND** `LastEvaluatedKey` SHALL be set if more items exist

### Requirement: Scan Operations via SDK
The server SHALL support scan operations with optional filtering and pagination.

#### Scenario: Scan all items
- **WHEN** `ScanAsync` is called with no filter
- **THEN** the response SHALL contain all items in the table

#### Scenario: Scan with filter expression
- **WHEN** `ScanAsync` is called with a `FilterExpression`
- **THEN** the response SHALL contain only matching items

#### Scenario: Scan with limit
- **WHEN** `ScanAsync` is called with a `Limit`
- **THEN** at most `Limit` items SHALL be returned

#### Scenario: Scan with pagination
- **WHEN** multiple `ScanAsync` calls are made using `ExclusiveStartKey` from previous `LastEvaluatedKey`
- **THEN** all items SHALL eventually be returned across pages

### Requirement: UpdateItem via SDK
The server SHALL support update operations with expressions when invoked through the AWS SDK v4 client.

#### Scenario: SET attribute
- **GIVEN** an item exists
- **WHEN** `UpdateItemAsync` is called with `UpdateExpression = "SET #n = :val"`
- **THEN** the attribute SHALL be updated to the new value

#### Scenario: Arithmetic update
- **GIVEN** an item has a numeric attribute
- **WHEN** `UpdateItemAsync` is called with `SET #c = #c + :inc`
- **THEN** the attribute SHALL be incremented

#### Scenario: REMOVE attribute
- **GIVEN** an item has a non-key attribute
- **WHEN** `UpdateItemAsync` is called with `REMOVE temp`
- **THEN** the attribute SHALL be removed from the item

#### Scenario: Condition check fails
- **GIVEN** an item exists with `status = "active"`
- **WHEN** `UpdateItemAsync` is called with `ConditionExpression = "#s = :expected"` where `:expected` is `"wrong"`
- **THEN** the SDK SHALL throw `ConditionalCheckFailedException`

#### Scenario: Return ALL_NEW
- **WHEN** `UpdateItemAsync` is called with `ReturnValues = ALL_NEW`
- **THEN** the response SHALL contain all attributes of the item after the update

#### Scenario: Upsert (update non-existent item)
- **WHEN** `UpdateItemAsync` is called with a key that does not exist
- **THEN** a new item SHALL be created with the key and SET attributes

#### Scenario: ADD to string set
- **GIVEN** an item has a string set attribute
- **WHEN** `UpdateItemAsync` is called with `ADD tags :newTags`
- **THEN** the new values SHALL be added to the set

#### Scenario: DELETE from string set
- **GIVEN** an item has a string set attribute
- **WHEN** `UpdateItemAsync` is called with `DELETE tags :removeTags`
- **THEN** the specified values SHALL be removed from the set

### Requirement: Batch Operations via SDK
The server SHALL support batch operations when invoked through the AWS SDK v4 client.

#### Scenario: BatchWriteItem puts multiple items
- **WHEN** `BatchWriteItemAsync` is called with multiple put requests
- **THEN** all items SHALL be stored
- **AND** `UnprocessedItems` SHALL be empty

#### Scenario: BatchGetItem retrieves multiple items
- **GIVEN** multiple items exist
- **WHEN** `BatchGetItemAsync` is called with their keys (including a non-existent key)
- **THEN** existing items SHALL be returned in the response
- **AND** `UnprocessedKeys` SHALL be empty

### Requirement: Transaction Operations via SDK
The server SHALL support transactional operations when invoked through the AWS SDK v4 client.

#### Scenario: TransactWriteItems all succeed
- **WHEN** `TransactWriteItemsAsync` is called with multiple put operations
- **THEN** all items SHALL be written atomically

#### Scenario: TransactWriteItems condition fails
- **GIVEN** a condition check references an item with unexpected attribute values
- **WHEN** `TransactWriteItemsAsync` is called
- **THEN** the SDK SHALL throw `TransactionCanceledException`
- **AND** no items from the transaction SHALL be written

#### Scenario: TransactGetItems retrieves items
- **GIVEN** multiple items exist
- **WHEN** `TransactGetItemsAsync` is called with their keys
- **THEN** the response SHALL contain items for existing keys
- **AND** empty items for non-existent keys

### Requirement: Local Secondary Index via SDK
The server SHALL support querying local secondary indexes through the AWS SDK v4 client.

#### Scenario: Query on LSI returns items sorted by index sort key
- **GIVEN** a table with an LSI on attribute `lsiSk`
- **WHEN** `QueryAsync` is called with `IndexName` set to the LSI name
- **THEN** items SHALL be returned sorted by the `lsiSk` attribute
- **AND** items without the `lsiSk` attribute SHALL be excluded

#### Scenario: Query on LSI with sort key condition
- **WHEN** `QueryAsync` is called with `IndexName` and a key condition on `lsiSk`
- **THEN** only items matching the key condition SHALL be returned

#### Scenario: Query on LSI in reverse order
- **WHEN** `QueryAsync` is called with `IndexName` and `ScanIndexForward = false`
- **THEN** items SHALL be returned in descending order of `lsiSk`

#### Scenario: Query on LSI returns all projected attributes
- **GIVEN** the LSI uses `ProjectionType.ALL`
- **WHEN** items are queried through the LSI
- **THEN** each item SHALL contain all attributes from the base table

#### Scenario: Query on non-existent index
- **WHEN** `QueryAsync` is called with an `IndexName` that does not exist
- **THEN** the SDK SHALL throw `AmazonDynamoDBException` indicating the index is not found

### Requirement: CI/CD Integration
The integration tests SHALL run as part of a continuous integration pipeline.

#### Scenario: Tests run on pull requests
- **WHEN** a pull request is opened or updated against the main branch
- **THEN** the CI pipeline SHALL build the solution and run all integration tests

#### Scenario: Tests run on main branch pushes
- **WHEN** code is pushed to the main branch
- **THEN** the CI pipeline SHALL build the solution and run all integration tests

#### Scenario: Tests require no external services
- **GIVEN** the tests use `WebApplicationFactory` for in-process hosting
- **WHEN** the CI pipeline runs the tests
- **THEN** no DynamoDB instance or AWS credentials SHALL be required
