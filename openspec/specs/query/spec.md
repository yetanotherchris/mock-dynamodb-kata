# Query Specification

## Purpose
The server SHALL support Query operations with KeyConditionExpression.

## Requirements

### Requirement: KeyConditionExpression
The server SHALL parse and evaluate key condition expressions.

#### Scenario: Partition key only
- **GIVEN** table with HASH "pk", RANGE "sk"
- **WHEN** Query with KeyConditionExpression "pk = :pk"
- **THEN** the server SHALL return all items with that partition key

#### Scenario: Partition key and sort key equality
- **WHEN** KeyConditionExpression is "pk = :pk AND sk = :sk"
- **THEN** the server SHALL return items matching both

#### Scenario: Sort key begins_with
- **WHEN** KeyConditionExpression is "pk = :pk AND begins_with(sk, :prefix)"
- **THEN** the server SHALL return items where sk starts with the prefix

#### Scenario: Sort key range (BETWEEN)
- **WHEN** KeyConditionExpression is "pk = :pk AND sk BETWEEN :low AND :high"
- **THEN** the server SHALL return items where sk is within the range inclusive

#### Scenario: Sort key comparison operators
- **WHEN** KeyConditionExpression uses <, <=, >, >= on the sort key
- **THEN** the server SHALL filter accordingly

### Requirement: ScanIndexForward
The server SHALL control sort order via ScanIndexForward.

#### Scenario: Forward scan (default)
- **WHEN** ScanIndexForward is true or omitted
- **THEN** results SHALL be in ascending sort key order

#### Scenario: Reverse scan
- **WHEN** ScanIndexForward is false
- **THEN** results SHALL be in descending sort key order

### Requirement: Pagination
The server SHALL support Limit and ExclusiveStartKey for pagination.

#### Scenario: Limit applied before FilterExpression
- **GIVEN** 10 items match the key condition, 5 match the filter
- **WHEN** Limit is 3
- **THEN** the server SHALL evaluate 3 items from the key condition results and return matching ones

#### Scenario: ExclusiveStartKey
- **WHEN** ExclusiveStartKey is provided from a previous response
- **THEN** the server SHALL resume from after that key

#### Scenario: LastEvaluatedKey returned
- **WHEN** more items remain beyond the Limit
- **THEN** the response SHALL include LastEvaluatedKey

### Requirement: FilterExpression
The server SHALL apply FilterExpression after key condition evaluation.

#### Scenario: Filter reduces results
- **GIVEN** 5 items match key condition, 2 match filter
- **WHEN** Query with FilterExpression
- **THEN** only 2 items SHALL be returned but Count reflects filtered count

### Requirement: ProjectionExpression on Query
The server SHALL apply ProjectionExpression to each returned item.

### Requirement: Select
The server SHALL support Select parameter: ALL_ATTRIBUTES, ALL_PROJECTED_ATTRIBUTES, COUNT, SPECIFIC_ATTRIBUTES.

#### Scenario: Select COUNT
- **WHEN** Select is "COUNT"
- **THEN** the response SHALL include Count and ScannedCount but no Items
