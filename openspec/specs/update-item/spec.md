# UpdateItem Specification

## Purpose
The server SHALL support UpdateItem with UpdateExpression syntax.

## Requirements

### Requirement: SET Action
The server SHALL support SET clauses in UpdateExpression.

#### Scenario: Set a single attribute
- **GIVEN** an item with pk="id1" exists
- **WHEN** UpdateItem is called with UpdateExpression "SET #a = :val"
- **THEN** the server SHALL set the attribute to the specified value

#### Scenario: Set multiple attributes
- **WHEN** UpdateExpression is "SET a = :v1, b = :v2"
- **THEN** the server SHALL set both attributes

#### Scenario: SET with arithmetic
- **GIVEN** item has attr "count" with N="5"
- **WHEN** UpdateExpression is "SET count = count + :inc" with :inc = N "1"
- **THEN** count SHALL become N "6"

#### Scenario: SET with if_not_exists
- **GIVEN** item does not have attr "views"
- **WHEN** UpdateExpression is "SET views = if_not_exists(views, :zero)" with :zero = N "0"
- **THEN** views SHALL be set to N "0"

#### Scenario: SET with list_append
- **GIVEN** item has attr "tags" with L [{"S":"a"}]
- **WHEN** UpdateExpression is "SET tags = list_append(tags, :newTags)" with :newTags = L [{"S":"b"}]
- **THEN** tags SHALL become L [{"S":"a"}, {"S":"b"}]

### Requirement: REMOVE Action
The server SHALL support REMOVE clauses to delete attributes.

#### Scenario: Remove an attribute
- **WHEN** UpdateExpression is "REMOVE someAttr"
- **THEN** the server SHALL remove the attribute from the item

### Requirement: ADD Action
The server SHALL support ADD clauses for numbers and sets.

#### Scenario: ADD to a number
- **GIVEN** item has "count" = N "5"
- **WHEN** UpdateExpression is "ADD count :inc" with :inc = N "3"
- **THEN** count SHALL become N "8"

#### Scenario: ADD to a set
- **GIVEN** item has "tags" = SS ["a", "b"]
- **WHEN** UpdateExpression is "ADD tags :newTags" with :newTags = SS ["c"]
- **THEN** tags SHALL become SS ["a", "b", "c"]

### Requirement: DELETE Action
The server SHALL support DELETE clauses for removing elements from sets.

#### Scenario: DELETE from a set
- **GIVEN** item has "tags" = SS ["a", "b", "c"]
- **WHEN** UpdateExpression is "DELETE tags :removeTags" with :removeTags = SS ["b"]
- **THEN** tags SHALL become SS ["a", "c"]

### Requirement: ReturnValues
The server SHALL support ReturnValues: NONE, ALL_OLD, UPDATED_OLD, ALL_NEW, UPDATED_NEW.

#### Scenario: ReturnValues ALL_NEW
- **WHEN** UpdateItem is called with ReturnValues "ALL_NEW"
- **THEN** the server SHALL return the complete item after the update in the Attributes field

### Requirement: ConditionExpression on UpdateItem
The server SHALL evaluate ConditionExpression before applying the update.

#### Scenario: Condition fails
- **GIVEN** item has "status" = S "active"
- **WHEN** UpdateItem has ConditionExpression "status = :expected" with :expected = S "inactive"
- **THEN** the server SHALL return ConditionalCheckFailedException

### Requirement: Create on Update
The server SHALL create the item if it doesn't exist (upsert behavior).

#### Scenario: Update non-existent item
- **WHEN** UpdateItem is called for a key that doesn't exist
- **THEN** the server SHALL create a new item with the key and applied update expressions
