# Transactions Specification

## Purpose
The server SHALL support TransactWriteItems and TransactGetItems for atomic operations.

## Requirements

### Requirement: TransactWriteItems
The server SHALL execute Put, Update, Delete, and ConditionCheck actions atomically.

#### Scenario: All actions succeed
- **WHEN** TransactWriteItems with 3 Put actions all succeed
- **THEN** all items SHALL be written atomically

#### Scenario: Condition check fails
- **GIVEN** a ConditionCheck on an item that does not meet the condition
- **WHEN** TransactWriteItems is called
- **THEN** the server SHALL return TransactionCanceledException
- **AND** no actions SHALL be applied
- **AND** CancellationReasons SHALL indicate which item failed

#### Scenario: Maximum 100 actions
- **WHEN** TransactWriteItems includes more than 100 actions
- **THEN** the server SHALL return ValidationException

#### Scenario: Duplicate items in transaction
- **WHEN** two actions in the same transaction target the same item
- **THEN** the server SHALL return ValidationException

#### Scenario: Mixed action types
- **WHEN** TransactWriteItems includes Put, Update, Delete, and ConditionCheck
- **THEN** all SHALL be evaluated and applied atomically

### Requirement: TransactGetItems
The server SHALL read multiple items atomically.

#### Scenario: Get multiple items
- **WHEN** TransactGetItems with 3 Get actions
- **THEN** the server SHALL return all items in Responses

#### Scenario: Item not found
- **WHEN** a Get action targets a non-existent item
- **THEN** that entry in Responses SHALL have an empty Item

#### Scenario: Maximum 100 items
- **WHEN** TransactGetItems includes more than 100 items
- **THEN** the server SHALL return ValidationException

### Requirement: Transaction Isolation
Transactions SHALL use ReaderWriterLockSlim for isolation.

#### Scenario: Transactions block concurrent writes
- **WHEN** a transaction is in progress
- **THEN** other write operations SHALL wait (writer lock)
- **AND** read operations SHALL proceed (reader lock for normal ops)
