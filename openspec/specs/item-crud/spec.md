# Item CRUD Specification

## Purpose
The server SHALL support basic item operations: PutItem, GetItem, DeleteItem.

## Requirements

### Requirement: PutItem
The server SHALL store an item in the specified table, replacing any existing item with the same key.

#### Scenario: Put item in hash-key table
- **GIVEN** table "TestTable" exists with HASH key "pk" (S)
- **WHEN** PutItem is called with Item {"pk": {"S": "id1"}, "data": {"S": "hello"}}
- **THEN** the server SHALL store the item

#### Scenario: Put item in hash-range table
- **GIVEN** table "TestTable" exists with HASH "pk" (S), RANGE "sk" (S)
- **WHEN** PutItem is called with Item including both pk and sk
- **THEN** the server SHALL store the item keyed by both values

#### Scenario: Put replaces existing item
- **GIVEN** an item with pk="id1" already exists
- **WHEN** PutItem is called with pk="id1" and different attributes
- **THEN** the server SHALL replace the entire item

#### Scenario: Missing key attributes
- **WHEN** PutItem is called without the required key attributes
- **THEN** the server SHALL return ValidationException

#### Scenario: Table does not exist
- **WHEN** PutItem is called with a non-existent TableName
- **THEN** the server SHALL return ResourceNotFoundException

### Requirement: GetItem
The server SHALL retrieve an item by its primary key.

#### Scenario: Get existing item
- **GIVEN** an item with pk="id1" exists in "TestTable"
- **WHEN** GetItem is called with Key {"pk": {"S": "id1"}}
- **THEN** the server SHALL return the full item in the Item field

#### Scenario: Get non-existent item
- **GIVEN** no item with pk="missing" exists
- **WHEN** GetItem is called with Key {"pk": {"S": "missing"}}
- **THEN** the server SHALL return an empty response (no Item field)

#### Scenario: Get with ConsistentRead
- **WHEN** GetItem is called with ConsistentRead true
- **THEN** the server SHALL accept the parameter (no behavioral difference in mock)

### Requirement: DeleteItem
The server SHALL remove an item by its primary key.

#### Scenario: Delete existing item
- **GIVEN** an item with pk="id1" exists
- **WHEN** DeleteItem is called with Key {"pk": {"S": "id1"}}
- **THEN** the server SHALL remove the item

#### Scenario: Delete non-existent item
- **WHEN** DeleteItem is called with a key that doesn't exist
- **THEN** the server SHALL succeed (no error)

#### Scenario: Delete with ReturnValues ALL_OLD
- **GIVEN** an item with pk="id1" exists
- **WHEN** DeleteItem is called with ReturnValues "ALL_OLD"
- **THEN** the server SHALL return the deleted item in the Attributes field

### Requirement: AttributeValue Types
The server SHALL support all DynamoDB attribute value types: S, N, B, BOOL, NULL, L, M, SS, NS, BS.

#### Scenario: Store and retrieve all types
- **WHEN** PutItem is called with attributes of each type
- **THEN** GetItem SHALL return all attributes with their exact types preserved
