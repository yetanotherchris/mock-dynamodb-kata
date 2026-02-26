# Secondary Index Specification

## Purpose
The server SHALL support Local Secondary Indexes (LSIs) and Global Secondary Indexes (GSIs) on tables.

## Requirements

### Requirement: LSI Creation
LSIs SHALL be defined at CreateTable time and cannot be modified afterwards.

#### Scenario: Create table with LSI
- **WHEN** CreateTable includes LocalSecondaryIndexes with same HASH key and different RANGE key
- **THEN** the server SHALL create the table with the LSI

#### Scenario: LSI must share partition key
- **WHEN** an LSI specifies a different HASH key than the table
- **THEN** the server SHALL return ValidationException

#### Scenario: Maximum 5 LSIs
- **WHEN** CreateTable includes more than 5 LSIs
- **THEN** the server SHALL return ValidationException

#### Scenario: LSI attribute must be in AttributeDefinitions
- **WHEN** an LSI range key is not in AttributeDefinitions
- **THEN** the server SHALL return ValidationException

### Requirement: LSI Index Maintenance
The server SHALL maintain LSI data structures on every write.

#### Scenario: Item with LSI sort key is indexed
- **GIVEN** table with LSI on "lsiSk"
- **WHEN** PutItem includes "lsiSk" attribute
- **THEN** the item SHALL be queryable via the LSI

#### Scenario: Item without LSI sort key is excluded
- **WHEN** PutItem does not include the LSI sort key attribute
- **THEN** the item SHALL NOT appear in LSI queries

### Requirement: Query with IndexName (LSI)
The server SHALL support querying an LSI by specifying IndexName.

#### Scenario: Query LSI
- **GIVEN** items indexed by LSI
- **WHEN** Query with IndexName set to the LSI name
- **THEN** results SHALL be sorted by the LSI sort key

### Requirement: GSI Creation
GSIs SHALL be defined at CreateTable time.

#### Scenario: Create table with GSI
- **WHEN** CreateTable includes GlobalSecondaryIndexes with a HASH key defined in AttributeDefinitions
- **THEN** the server SHALL create the table with the GSI and DescribeTable SHALL show IndexStatus=ACTIVE

#### Scenario: GSI hash key must be in AttributeDefinitions
- **WHEN** a GSI specifies a HASH key not present in AttributeDefinitions
- **THEN** the server SHALL return ValidationException

#### Scenario: GSI range key must be in AttributeDefinitions
- **WHEN** a GSI specifies a RANGE key not present in AttributeDefinitions
- **THEN** the server SHALL return ValidationException

#### Scenario: Maximum 20 GSIs
- **WHEN** CreateTable includes more than 20 GSIs
- **THEN** the server SHALL return ValidationException

#### Scenario: GSI range key is optional
- **WHEN** a GSI specifies only a HASH key (no RANGE key)
- **THEN** the server SHALL create the GSI and querying by hash key SHALL return all matching items

### Requirement: GSI Index Maintenance
The server SHALL maintain GSI data structures on every write.

#### Scenario: Item with GSI hash key is indexed
- **WHEN** PutItem includes the GSI hash key attribute
- **THEN** the item SHALL be queryable via the GSI

#### Scenario: Item without GSI hash key is excluded
- **WHEN** PutItem does not include the GSI hash key attribute
- **THEN** the item SHALL NOT appear in GSI queries

#### Scenario: UpdateItem changes GSI partition
- **WHEN** PutItem overwrites an item with a different GSI hash key value
- **THEN** the old entry SHALL be removed from the old GSI partition and added to the new one

#### Scenario: DeleteItem removes from GSI
- **WHEN** DeleteItem removes an item that had a GSI hash key
- **THEN** the item SHALL no longer appear in GSI queries

### Requirement: Query with IndexName (GSI)
The server SHALL support querying a GSI by specifying IndexName.

#### Scenario: Query GSI by hash key
- **GIVEN** items indexed by GSI
- **WHEN** Query with IndexName set to the GSI name and KeyConditionExpression on the GSI hash key
- **THEN** results SHALL be returned sorted by the GSI sort key (if present)

#### Scenario: Query GSI with sort key condition
- **WHEN** Query on a GSI includes a sort key condition
- **THEN** only items matching the sort key condition SHALL be returned

#### Scenario: Query non-existent index
- **WHEN** Query specifies an IndexName that is neither an LSI nor a GSI
- **THEN** the server SHALL return ValidationException with "does not have the specified index"

### Requirement: Projection Types
The server SHALL support projection types: KEYS_ONLY, INCLUDE, ALL.

#### Scenario: KEYS_ONLY projection
- **WHEN** LSI/GSI has ProjectionType KEYS_ONLY
- **THEN** Query on the index SHALL return only table key and index key attributes

#### Scenario: INCLUDE projection
- **WHEN** LSI/GSI has ProjectionType INCLUDE with NonKeyAttributes ["attr1"]
- **THEN** Query SHALL return keys plus attr1

#### Scenario: ALL projection
- **WHEN** LSI/GSI has ProjectionType ALL
- **THEN** Query SHALL return all item attributes
