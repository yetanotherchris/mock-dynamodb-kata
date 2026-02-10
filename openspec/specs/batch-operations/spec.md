# Batch Operations Specification

## Purpose
The server SHALL support BatchGetItem and BatchWriteItem operations.

## Requirements

### Requirement: BatchGetItem
The server SHALL retrieve multiple items from one or more tables in a single call.

#### Scenario: Get items from single table
- **GIVEN** items exist in "TestTable"
- **WHEN** BatchGetItem with Keys for 3 items
- **THEN** the server SHALL return all 3 items in Responses["TestTable"]

#### Scenario: Get items from multiple tables
- **WHEN** BatchGetItem requests items from "Table1" and "Table2"
- **THEN** the server SHALL return items grouped by table name

#### Scenario: Some keys not found
- **WHEN** some requested keys do not exist
- **THEN** those keys SHALL simply be absent from the response (no error)

#### Scenario: Maximum 100 keys
- **WHEN** BatchGetItem includes more than 100 keys total
- **THEN** the server SHALL return ValidationException

#### Scenario: UnprocessedKeys
- The server SHALL always return empty UnprocessedKeys (no throttling simulation)

#### Scenario: ConsistentRead per table
- **WHEN** ConsistentRead is specified per table
- **THEN** the server SHALL accept the parameter

#### Scenario: ProjectionExpression per table
- **WHEN** ProjectionExpression is specified per table
- **THEN** the server SHALL apply projection to returned items

### Requirement: BatchWriteItem
The server SHALL write or delete multiple items across tables.

#### Scenario: Put and delete in single call
- **WHEN** BatchWriteItem includes PutRequest and DeleteRequest items
- **THEN** the server SHALL execute all operations

#### Scenario: Maximum 25 items
- **WHEN** BatchWriteItem includes more than 25 requests total
- **THEN** the server SHALL return ValidationException

#### Scenario: UnprocessedItems
- The server SHALL always return empty UnprocessedItems

#### Scenario: Table does not exist
- **WHEN** BatchWriteItem references a non-existent table
- **THEN** the server SHALL return ResourceNotFoundException

#### Scenario: Duplicate keys in same batch
- **WHEN** BatchWriteItem includes two operations on the same key in the same table
- **THEN** the server SHALL return ValidationException
