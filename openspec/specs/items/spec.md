# Items Specification

## Purpose

Define the behavior of item-level operations: PutItem, GetItem, DeleteItem, and UpdateItem.

---

## PutItem

### Requirement: PutItem

The server SHALL store an item in the specified table, replacing any existing item with the same key.

#### Scenario: Put item in hash-key table

- GIVEN table "TestTable" exists with HASH key "pk" (S)
- WHEN PutItem is called with Item `{"pk": {"S": "id1"}, "data": {"S": "hello"}}`
- THEN the server SHALL store the item

#### Scenario: Put item in hash-range table

- GIVEN table "TestTable" exists with HASH "pk" (S), RANGE "sk" (S)
- WHEN PutItem is called with Item including both pk and sk
- THEN the server SHALL store the item keyed by both values

#### Scenario: Put replaces existing item

- GIVEN an item with pk="id1" already exists
- WHEN PutItem is called with pk="id1" and different attributes
- THEN the server SHALL replace the entire item

#### Scenario: Missing key attributes

- WHEN PutItem is called without the required key attributes
- THEN the server SHALL return ValidationException

#### Scenario: Table does not exist

- WHEN PutItem is called with a non-existent TableName
- THEN the server SHALL return ResourceNotFoundException

---

## GetItem

### Requirement: GetItem

The server SHALL retrieve an item by its primary key.

#### Scenario: Get existing item

- GIVEN an item with pk="id1" exists in "TestTable"
- WHEN GetItem is called with Key `{"pk": {"S": "id1"}}`
- THEN the server SHALL return the full item in the Item field

#### Scenario: Get non-existent item

- GIVEN no item with pk="missing" exists
- WHEN GetItem is called with Key `{"pk": {"S": "missing"}}`
- THEN the server SHALL return an empty response (no Item field)

#### Scenario: Get with ConsistentRead

- WHEN GetItem is called with ConsistentRead true
- THEN the server SHALL accept the parameter (no behavioral difference in mock)

---

## DeleteItem

### Requirement: DeleteItem

The server SHALL remove an item by its primary key.

#### Scenario: Delete existing item

- GIVEN an item with pk="id1" exists
- WHEN DeleteItem is called with Key `{"pk": {"S": "id1"}}`
- THEN the server SHALL remove the item

#### Scenario: Delete non-existent item

- WHEN DeleteItem is called with a key that doesn't exist
- THEN the server SHALL succeed (no error)

#### Scenario: Delete with ReturnValues ALL_OLD

- GIVEN an item with pk="id1" exists
- WHEN DeleteItem is called with ReturnValues "ALL_OLD"
- THEN the server SHALL return the deleted item in the Attributes field

---

## UpdateItem

### Requirement: SET Action

The server SHALL support SET clauses in UpdateExpression.

#### Scenario: Set a single attribute

- GIVEN an item with pk="id1" exists
- WHEN UpdateItem is called with UpdateExpression `"SET #a = :val"`
- THEN the server SHALL set the attribute to the specified value

#### Scenario: Set multiple attributes

- WHEN UpdateExpression is `"SET a = :v1, b = :v2"`
- THEN the server SHALL set both attributes

#### Scenario: SET with arithmetic

- GIVEN item has attr "count" with N="5"
- WHEN UpdateExpression is `"SET count = count + :inc"` with `:inc = N "1"`
- THEN count SHALL become N "6"

#### Scenario: SET with if_not_exists

- GIVEN item does not have attr "views"
- WHEN UpdateExpression is `"SET views = if_not_exists(views, :zero)"` with `:zero = N "0"`
- THEN views SHALL be set to N "0"

#### Scenario: SET with list_append

- GIVEN item has attr "tags" with L `[{"S":"a"}]`
- WHEN UpdateExpression is `"SET tags = list_append(tags, :newTags)"` with `:newTags = L [{"S":"b"}]`
- THEN tags SHALL become L `[{"S":"a"}, {"S":"b"}]`

### Requirement: REMOVE Action

The server SHALL support REMOVE clauses to delete attributes.

#### Scenario: Remove an attribute

- WHEN UpdateExpression is `"REMOVE someAttr"`
- THEN the server SHALL remove the attribute from the item

### Requirement: ADD Action

The server SHALL support ADD clauses for numbers and sets.

#### Scenario: ADD to a number

- GIVEN item has "count" = N "5"
- WHEN UpdateExpression is `"ADD count :inc"` with `:inc = N "3"`
- THEN count SHALL become N "8"

#### Scenario: ADD to a set

- GIVEN item has "tags" = SS ["a", "b"]
- WHEN UpdateExpression is `"ADD tags :newTags"` with `:newTags = SS ["c"]`
- THEN tags SHALL become SS ["a", "b", "c"]

### Requirement: DELETE Action

The server SHALL support DELETE clauses for removing elements from sets.

#### Scenario: DELETE from a set

- GIVEN item has "tags" = SS ["a", "b", "c"]
- WHEN UpdateExpression is `"DELETE tags :removeTags"` with `:removeTags = SS ["b"]`
- THEN tags SHALL become SS ["a", "c"]

### Requirement: ReturnValues

The server SHALL support ReturnValues: NONE, ALL_OLD, UPDATED_OLD, ALL_NEW, UPDATED_NEW.

#### Scenario: ReturnValues ALL_NEW

- WHEN UpdateItem is called with ReturnValues "ALL_NEW"
- THEN the server SHALL return the complete item after the update in the Attributes field

### Requirement: ConditionExpression on UpdateItem

The server SHALL evaluate ConditionExpression before applying the update.

#### Scenario: Condition fails

- GIVEN item has "status" = S "active"
- WHEN UpdateItem has ConditionExpression `"status = :expected"` with `:expected = S "inactive"`
- THEN the server SHALL return ConditionalCheckFailedException

### Requirement: Upsert Behavior

The server SHALL create the item if it doesn't exist.

#### Scenario: Update non-existent item

- WHEN UpdateItem is called for a key that doesn't exist
- THEN the server SHALL create a new item with the key and applied update expressions

---

## Attribute Value Types

### Requirement: AttributeValue Types

The server SHALL support all DynamoDB attribute value types: S, N, B, BOOL, NULL, L, M, SS, NS, BS.

#### Scenario: Store and retrieve all types

- WHEN PutItem is called with attributes of each type
- THEN GetItem SHALL return all attributes with their exact types preserved
