# Local Secondary Index Specification

## Purpose
The server SHALL support Local Secondary Indexes (LSIs) on tables.

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

### Requirement: Query with IndexName
The server SHALL support querying an LSI by specifying IndexName.

#### Scenario: Query LSI
- **GIVEN** items indexed by LSI
- **WHEN** Query with IndexName set to the LSI name
- **THEN** results SHALL be sorted by the LSI sort key

### Requirement: Projection Types
The server SHALL support projection types: KEYS_ONLY, INCLUDE, ALL.

#### Scenario: KEYS_ONLY projection
- **WHEN** LSI has ProjectionType KEYS_ONLY
- **THEN** Query on the LSI SHALL return only table key and LSI key attributes

#### Scenario: INCLUDE projection
- **WHEN** LSI has ProjectionType INCLUDE with NonKeyAttributes ["attr1"]
- **THEN** Query SHALL return keys plus attr1

#### Scenario: ALL projection
- **WHEN** LSI has ProjectionType ALL
- **THEN** Query SHALL return all item attributes
