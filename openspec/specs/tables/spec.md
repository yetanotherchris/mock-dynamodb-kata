# Table Operations Specification

## Purpose
The server SHALL support creating, deleting, describing, and listing tables.

## Requirements

### Requirement: CreateTable
The server SHALL create a table with the specified name, key schema, and attribute definitions.

#### Scenario: Create a simple hash-key table
- **GIVEN** no table named "TestTable" exists
- **WHEN** CreateTable is called with TableName "TestTable", KeySchema [HASH key "pk"], AttributeDefinitions [pk as S]
- **THEN** the server SHALL create the table
- **AND** return TableDescription with TableStatus "ACTIVE"

#### Scenario: Create a table with hash and range key
- **GIVEN** no table named "TestTable" exists
- **WHEN** CreateTable is called with KeySchema [HASH "pk", RANGE "sk"] and matching AttributeDefinitions
- **THEN** the server SHALL create the table with both keys

#### Scenario: Table already exists
- **GIVEN** a table named "TestTable" already exists
- **WHEN** CreateTable is called with TableName "TestTable"
- **THEN** the server SHALL return ResourceInUseException

#### Scenario: Missing key attribute definition
- **WHEN** CreateTable is called with a key not present in AttributeDefinitions
- **THEN** the server SHALL return ValidationException

### Requirement: DeleteTable
The server SHALL delete a table and all its items.

#### Scenario: Delete existing table
- **GIVEN** a table named "TestTable" exists
- **WHEN** DeleteTable is called with TableName "TestTable"
- **THEN** the server SHALL remove the table
- **AND** return TableDescription of the deleted table

#### Scenario: Delete non-existent table
- **WHEN** DeleteTable is called with a TableName that does not exist
- **THEN** the server SHALL return ResourceNotFoundException

### Requirement: DescribeTable
The server SHALL return the full definition of a table.

#### Scenario: Describe existing table
- **GIVEN** a table named "TestTable" exists
- **WHEN** DescribeTable is called with TableName "TestTable"
- **THEN** the server SHALL return Table with TableName, KeySchema, AttributeDefinitions, TableStatus "ACTIVE", CreationDateTime, ItemCount, TableSizeBytes

#### Scenario: Describe non-existent table
- **WHEN** DescribeTable is called with a TableName that does not exist
- **THEN** the server SHALL return ResourceNotFoundException

### Requirement: ListTables
The server SHALL list all table names with optional pagination.

#### Scenario: List all tables
- **GIVEN** tables "Alpha", "Beta", "Gamma" exist
- **WHEN** ListTables is called with no parameters
- **THEN** the server SHALL return TableNames ["Alpha", "Beta", "Gamma"] sorted alphabetically

#### Scenario: List tables with limit
- **GIVEN** tables "A", "B", "C" exist
- **WHEN** ListTables is called with Limit 2
- **THEN** the server SHALL return TableNames ["A", "B"] and LastEvaluatedTableName "B"

#### Scenario: List tables with ExclusiveStartTableName
- **GIVEN** tables "A", "B", "C" exist
- **WHEN** ListTables is called with ExclusiveStartTableName "A"
- **THEN** the server SHALL return TableNames ["B", "C"]

#### Scenario: No tables exist
- **WHEN** ListTables is called with no tables created
- **THEN** the server SHALL return an empty TableNames array
