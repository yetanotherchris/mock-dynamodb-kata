# Scan Specification

## Purpose
The server SHALL support full table Scan operations with optional parallel scan.

## Requirements

### Requirement: Full Table Scan
The server SHALL iterate all items in deterministic order.

#### Scenario: Scan all items
- **GIVEN** a table with items
- **WHEN** Scan is called with no filter
- **THEN** the server SHALL return all items

#### Scenario: Deterministic order
- **WHEN** Scan is called multiple times
- **THEN** items SHALL be returned in the same order (sorted by partition key, then sort key)

### Requirement: FilterExpression on Scan
The server SHALL apply FilterExpression to each item during scan.

#### Scenario: Filter items
- **GIVEN** items with varying "status" attributes
- **WHEN** Scan with FilterExpression "status = :active"
- **THEN** only items matching the filter SHALL be returned

### Requirement: ProjectionExpression on Scan
The server SHALL apply ProjectionExpression to each returned item.

### Requirement: Pagination
The server SHALL support Limit and ExclusiveStartKey.

#### Scenario: Paginated scan
- **GIVEN** 100 items in the table
- **WHEN** Scan with Limit 25
- **THEN** 25 items SHALL be returned with LastEvaluatedKey

### Requirement: Parallel Scan
The server SHALL support TotalSegments and Segment parameters.

#### Scenario: Parallel scan segments
- **GIVEN** items in the table
- **WHEN** Scan with TotalSegments 4, Segment 0
- **THEN** the server SHALL return items assigned to segment 0

#### Scenario: Segment assignment
- Items SHALL be assigned to segments using FNV-1a hash of the serialized partition key modulo TotalSegments

#### Scenario: All segments cover all items
- **GIVEN** TotalSegments = N
- **WHEN** scanning all segments 0 through N-1
- **THEN** every item SHALL appear in exactly one segment

### Requirement: Select
The server SHALL support Select parameter analogous to Query.
