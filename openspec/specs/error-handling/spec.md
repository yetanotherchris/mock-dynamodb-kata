# Error Handling Specification

## Purpose
The server SHALL return errors in the DynamoDB JSON error format.

## Requirements

### Requirement: Error Response Format
All errors SHALL use the DynamoDB error format.

#### Scenario: Error response structure
- **WHEN** an error occurs
- **THEN** the response SHALL have HTTP status 400 (client errors) or 500 (server errors)
- **AND** the body SHALL contain `__type` with the fully qualified error type
- **AND** the body SHALL contain `Message` (or `message`) with a description

### Requirement: Error Types
The server SHALL return appropriate error types.

#### Scenario: ResourceNotFoundException
- **WHEN** an operation references a non-existent table
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#ResourceNotFoundException`

#### Scenario: ResourceInUseException
- **WHEN** CreateTable is called for an existing table
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#ResourceInUseException`

#### Scenario: ValidationException
- **WHEN** request parameters are invalid
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#ValidationException`

#### Scenario: ConditionalCheckFailedException
- **WHEN** a ConditionExpression evaluates to false
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#ConditionalCheckFailedException`

#### Scenario: TransactionCanceledException
- **WHEN** a transaction condition fails
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#TransactionCanceledException`
- **AND** CancellationReasons SHALL be included

#### Scenario: UnknownOperationException
- **WHEN** X-Amz-Target contains an unknown operation
- **THEN** `__type` SHALL be `com.amazonaws.dynamodb.v20120810#UnknownOperationException`
