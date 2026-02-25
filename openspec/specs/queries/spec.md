# Queries Specification

## Purpose

Define the behavior of Query and Scan operations.

---

## Query

### Requirement: KeyConditionExpression

The server SHALL parse and evaluate key condition expressions.

#### Scenario: Partition key only

- GIVEN table with HASH "pk", RANGE "sk"
- WHEN Query with KeyConditionExpression `"pk = :pk"`
- THEN the server SHALL return all items with that partition key

#### Scenario: Partition key and sort key equality

- WHEN KeyConditionExpression is `"pk = :pk AND sk = :sk"`
- THEN the server SHALL return items matching both

#### Scenario: Sort key begins_with

- WHEN KeyConditionExpression is `"pk = :pk AND begins_with(sk, :prefix)"`
- THEN the server SHALL return items where sk starts with the prefix

#### Scenario: Sort key range (BETWEEN)

- WHEN KeyConditionExpression is `"pk = :pk AND sk BETWEEN :low AND :high"`
- THEN the server SHALL return items where sk is within the range inclusive

#### Scenario: Sort key comparison operators

- WHEN KeyConditionExpression uses `<`, `<=`, `>`, `>=` on the sort key
- THEN the server SHALL filter accordingly

### Requirement: ScanIndexForward

The server SHALL control sort order via ScanIndexForward.

#### Scenario: Forward scan (default)

- WHEN ScanIndexForward is true or omitted
- THEN results SHALL be in ascending sort key order

#### Scenario: Reverse scan

- WHEN ScanIndexForward is false
- THEN results SHALL be in descending sort key order

### Requirement: Query Pagination

The server SHALL support Limit and ExclusiveStartKey for pagination.

#### Scenario: Limit applied before FilterExpression

- GIVEN 10 items match the key condition, 5 match the filter
- WHEN Limit is 3
- THEN the server SHALL evaluate 3 items from the key condition results and return matching ones

#### Scenario: ExclusiveStartKey

- WHEN ExclusiveStartKey is provided from a previous response
- THEN the server SHALL resume from after that key

#### Scenario: LastEvaluatedKey returned

- WHEN more items remain beyond the Limit
- THEN the response SHALL include LastEvaluatedKey

### Requirement: FilterExpression on Query

The server SHALL apply FilterExpression after key condition evaluation.

#### Scenario: Filter reduces results

- GIVEN 5 items match key condition, 2 match filter
- WHEN Query with FilterExpression
- THEN only 2 items SHALL be returned

### Requirement: ProjectionExpression on Query

The server SHALL apply ProjectionExpression to each returned item.

### Requirement: Select on Query

The server SHALL support the Select parameter: ALL_ATTRIBUTES, ALL_PROJECTED_ATTRIBUTES, COUNT, SPECIFIC_ATTRIBUTES.

#### Scenario: Select COUNT

- WHEN Select is "COUNT"
- THEN the response SHALL include Count and ScannedCount but no Items

### Requirement: ConsumedCapacity on Query

The server SHALL return ConsumedCapacity when ReturnConsumedCapacity is TOTAL or INDEXES.

#### Scenario: ConsumedCapacity returned

- WHEN Query is called with ReturnConsumedCapacity "TOTAL"
- THEN the response SHALL include a ConsumedCapacity object with TableName and CapacityUnits

---

## Scan

### Requirement: Full Table Scan

The server SHALL iterate all items in deterministic order.

#### Scenario: Scan all items

- GIVEN a table with items
- WHEN Scan is called with no filter
- THEN the server SHALL return all items

#### Scenario: Deterministic order

- WHEN Scan is called multiple times
- THEN items SHALL be returned in the same order (sorted by partition key, then sort key)

### Requirement: FilterExpression on Scan

The server SHALL apply FilterExpression to each item during scan.

#### Scenario: Filter items

- GIVEN items with varying "status" attributes
- WHEN Scan with FilterExpression `"status = :active"`
- THEN only items matching the filter SHALL be returned

### Requirement: ProjectionExpression on Scan

The server SHALL apply ProjectionExpression to each returned item.

### Requirement: Scan Pagination

The server SHALL support Limit and ExclusiveStartKey.

#### Scenario: Paginated scan

- GIVEN 100 items in the table
- WHEN Scan with Limit 25
- THEN 25 items SHALL be returned with LastEvaluatedKey

### Requirement: Parallel Scan

The server SHALL support TotalSegments and Segment parameters.

#### Scenario: Parallel scan segments

- GIVEN items in the table
- WHEN Scan with TotalSegments 4, Segment 0
- THEN the server SHALL return items assigned to segment 0

#### Scenario: Segment assignment

- Items SHALL be assigned to segments using FNV-1a hash of the serialized partition key modulo TotalSegments

#### Scenario: All segments cover all items

- GIVEN TotalSegments = N
- WHEN scanning all segments 0 through N-1
- THEN every item SHALL appear in exactly one segment

### Requirement: Select on Scan

The server SHALL support the Select parameter analogous to Query.
