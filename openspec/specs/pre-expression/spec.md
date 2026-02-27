# Pre-Expression Format Specification

## Purpose

Define support for the pre-expression DynamoDB request format across Query, Scan,
UpdateItem, PutItem, and DeleteItem operations.

---

## Background: Two API Formats

DynamoDB supports two parallel formats for specifying conditions and updates. Both are
part of the official wire protocol and are accepted today.

### Expression Format (introduced 2014)

The modern API uses string expressions with named placeholders:

```json
{
  "KeyConditionExpression": "pk = :pkVal AND sk > :skVal",
  "FilterExpression": "category = :cat",
  "UpdateExpression": "SET price = :p REMOVE discount",
  "ConditionExpression": "attribute_exists(pk)",
  "ExpressionAttributeNames":  { "#n": "name" },
  "ExpressionAttributeValues": { ":pkVal": { "S": "abc" }, ":skVal": { "N": "10" } }
}
```

This format is recommended for all new code. It supports nested attribute paths,
name aliasing (`ExpressionAttributeNames`), and the full expression function library
(`begins_with`, `contains`, `attribute_exists`, `size`, etc.).

### Pre-Expression Format (original API, pre-2014)

The original API uses structured comparison objects — a map of attribute name to a
condition descriptor containing `ComparisonOperator` and `AttributeValueList`:

```json
{
  "KeyConditions": {
    "pk": { "AttributeValueList": [{ "S": "abc" }], "ComparisonOperator": "EQ" }
  },
  "ScanFilter": {
    "category": { "AttributeValueList": [{ "S": "food" }], "ComparisonOperator": "EQ" }
  },
  "AttributeUpdates": {
    "price": { "Value": { "N": "9.99" }, "Action": "PUT" }
  },
  "Expected": {
    "pk": { "Value": { "S": "abc" }, "Exists": true }
  }
}
```

This format does **not** support nested attribute paths or `ExpressionAttributeNames`
aliasing. Conditions apply only to top-level attributes by name.

### Who generates the pre-expression format

- `DynamoDBContext` (high-level .NET SDK ORM) — `QueryAsync`, `ScanAsync`,
  `SaveAsync`, `DeleteAsync` with version conditions all generate this format internally.
- Older SDK versions (v1/v2) that predate the expression API.
- Third-party tools targeting the pre-2014 API.

---

## KeyConditions (Query)

### Requirement: KeyConditions

The server SHALL accept `KeyConditions` as an alternative to `KeyConditionExpression`
in a Query request.

`KeyConditions` is a map of key attribute name to `{ AttributeValueList, ComparisonOperator }`.
The hash key MUST use `EQ`. The sort key MAY use any of the operators below.

#### Scenario: Hash key only via KeyConditions

- GIVEN table with HASH "pk" (S), RANGE "sk" (N)
- WHEN Query with `KeyConditions: { "pk": { "AttributeValueList": [{"S":"a"}], "ComparisonOperator": "EQ" } }`
- THEN the server SHALL return all items with pk = "a"

#### Scenario: Hash key and sort key EQ

- WHEN KeyConditions includes both pk (EQ) and sk (EQ)
- THEN the server SHALL return the single matching item

#### Scenario: Sort key operators

- WHEN KeyConditions uses LE, LT, GE, GT, BEGINS_WITH, or BETWEEN on the sort key
- THEN the server SHALL filter items using the corresponding comparison

#### Scenario: BETWEEN sort key

- WHEN KeyConditions sort key uses BETWEEN with two values
- THEN the server SHALL return items where sk is between the two values inclusive

#### Scenario: Hash key ComparisonOperator not EQ

- WHEN KeyConditions hash key uses any operator other than EQ
- THEN the server SHALL return ValidationException

---

## QueryFilter (Query)

### Requirement: QueryFilter

The server SHALL accept `QueryFilter` as an alternative to `FilterExpression` in a
Query request. It is applied after key condition evaluation (same as `FilterExpression`).

`QueryFilter` uses the same `{ AttributeValueList, ComparisonOperator }` structure as
`ScanFilter`. `ConditionalOperator` (AND/OR) controls how multiple conditions combine;
AND is the default.

#### Scenario: QueryFilter single condition

- GIVEN items matching the key condition with varying "status" values
- WHEN Query with `QueryFilter: { "status": { "AttributeValueList": [{"S":"active"}], "ComparisonOperator": "EQ" } }`
- THEN only items with status = "active" SHALL be returned

#### Scenario: QueryFilter with ConditionalOperator OR

- WHEN QueryFilter has two conditions and ConditionalOperator is "OR"
- THEN items matching either condition SHALL be returned

---

## ScanFilter (Scan)

### Requirement: ScanFilter

The server SHALL accept `ScanFilter` as an alternative to `FilterExpression` in a
Scan request. `ConditionalOperator` (AND/OR) controls how multiple conditions combine.

Supported `ComparisonOperator` values:

| Operator      | Description                                      | Values required |
|---------------|--------------------------------------------------|-----------------|
| EQ            | Equal                                            | 1               |
| NE            | Not equal                                        | 1               |
| LE            | Less than or equal                               | 1               |
| LT            | Less than                                        | 1               |
| GE            | Greater than or equal                            | 1               |
| GT            | Greater than                                     | 1               |
| NOT_NULL      | Attribute exists and is not null                 | 0               |
| NULL          | Attribute does not exist or is null              | 0               |
| CONTAINS      | String contains substring, or set contains value | 1               |
| NOT_CONTAINS  | Negation of CONTAINS                             | 1               |
| BEGINS_WITH   | String starts with prefix                        | 1               |
| IN            | Value is in the provided list                    | 1+              |
| BETWEEN       | Value is between two values inclusive            | 2               |

#### Scenario: ScanFilter EQ

- GIVEN a table with items having a "type" attribute
- WHEN Scan with `ScanFilter: { "type": { "AttributeValueList": [{"S":"polygon"}], "ComparisonOperator": "EQ" } }`
- THEN only items with type = "polygon" SHALL be returned

#### Scenario: ScanFilter NULL

- WHEN ScanFilter uses NULL (no AttributeValueList)
- THEN only items where the attribute does not exist SHALL be returned

#### Scenario: ScanFilter NOT_NULL

- WHEN ScanFilter uses NOT_NULL
- THEN only items where the attribute exists SHALL be returned

#### Scenario: ScanFilter CONTAINS on string

- WHEN ScanFilter uses CONTAINS on a string attribute
- THEN only items where the string contains the substring SHALL be returned

#### Scenario: ScanFilter IN

- WHEN ScanFilter uses IN with multiple values
- THEN items where the attribute equals any of the values SHALL be returned

#### Scenario: ScanFilter BETWEEN

- WHEN ScanFilter uses BETWEEN with two numeric values
- THEN only items with the attribute value between those numbers (inclusive) SHALL be returned

#### Scenario: Multiple conditions with AND (default)

- WHEN ScanFilter has multiple attribute conditions and no ConditionalOperator
- THEN all conditions MUST match (AND semantics)

#### Scenario: Multiple conditions with OR

- WHEN ScanFilter has multiple conditions and ConditionalOperator is "OR"
- THEN items matching any condition SHALL be returned

---

## AttributeUpdates (UpdateItem)

### Requirement: AttributeUpdates

The server SHALL accept `AttributeUpdates` as an alternative to `UpdateExpression` in
an UpdateItem request.

`AttributeUpdates` is a map of attribute name to `{ Value, Action }` where `Action` is
one of PUT, DELETE, or ADD. Action defaults to PUT when omitted.

| Action | With Value                        | Without Value        |
|--------|-----------------------------------|----------------------|
| PUT    | Set attribute to the given value  | N/A                  |
| DELETE | Remove elements from a set        | Remove the attribute |
| ADD    | Add number / union a set          | N/A                  |

#### Scenario: PUT sets attribute

- WHEN AttributeUpdates has `{ "price": { "Value": { "N": "9.99" }, "Action": "PUT" } }`
- THEN the price attribute SHALL be set to 9.99

#### Scenario: DELETE removes attribute

- WHEN AttributeUpdates has `{ "discount": { "Action": "DELETE" } }` (no Value)
- THEN the discount attribute SHALL be removed from the item

#### Scenario: DELETE removes set elements

- GIVEN item with `tags: { "SS": ["a", "b", "c"] }`
- WHEN AttributeUpdates has `{ "tags": { "Value": { "SS": ["b"] }, "Action": "DELETE" } }`
- THEN tags SHALL be `["a", "c"]`

#### Scenario: ADD increments a number

- GIVEN item with `count: { "N": "5" }`
- WHEN AttributeUpdates has `{ "count": { "Value": { "N": "3" }, "Action": "ADD" } }`
- THEN count SHALL be 8

#### Scenario: ADD creates attribute if absent

- WHEN ADD is used on an attribute that does not exist
- THEN the attribute SHALL be created with the given value

#### Scenario: ADD unions a set

- GIVEN item with `tags: { "SS": ["a", "b"] }`
- WHEN AttributeUpdates has `{ "tags": { "Value": { "SS": ["b", "c"] }, "Action": "ADD" } }`
- THEN tags SHALL be `["a", "b", "c"]`

---

## Expected (PutItem, UpdateItem, DeleteItem)

### Requirement: Expected

The server SHALL accept `Expected` as an alternative to `ConditionExpression` in
PutItem, UpdateItem, and DeleteItem requests.

`Expected` is a map of attribute name to a condition. Two forms are supported:

**Exists form** — checks presence or absence of an attribute:
```json
{ "pk": { "Exists": true, "Value": { "S": "abc" } } }
```

**Comparison form** — uses `ComparisonOperator` and `AttributeValueList`:
```json
{ "version": { "AttributeValueList": [{ "N": "3" }], "ComparisonOperator": "EQ" } }
```

`ConditionalOperator` (AND/OR) controls how multiple conditions combine; AND is the default.
If the condition fails, the server SHALL return `ConditionalCheckFailedException`.

#### Scenario: Exists true — attribute must exist

- WHEN Expected has `{ "pk": { "Exists": true } }`
- AND the item exists
- THEN the operation SHALL succeed

#### Scenario: Exists true — attribute must exist and equal value

- WHEN Expected has `{ "version": { "Value": { "N": "2" }, "Exists": true } }`
- AND the item has version = 2
- THEN the operation SHALL succeed

#### Scenario: Exists true fails when attribute absent

- WHEN Expected has `{ "pk": { "Exists": true } }`
- AND no item exists
- THEN the server SHALL return ConditionalCheckFailedException

#### Scenario: Exists false — attribute must not exist

- WHEN Expected has `{ "pk": { "Exists": false } }`
- AND the item does not exist
- THEN the operation SHALL succeed

#### Scenario: Exists false fails when attribute present

- WHEN Expected has `{ "pk": { "Exists": false } }`
- AND the item already exists
- THEN the server SHALL return ConditionalCheckFailedException

#### Scenario: Comparison form EQ

- WHEN Expected has `{ "version": { "AttributeValueList": [{"N":"3"}], "ComparisonOperator": "EQ" } }`
- AND version = 3
- THEN the operation SHALL succeed

#### Scenario: Condition fails

- WHEN any Expected condition is not met (with AND) or none are met (with OR)
- THEN the server SHALL return ConditionalCheckFailedException
