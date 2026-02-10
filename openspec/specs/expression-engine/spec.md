# Expression Engine Specification

## Purpose
The server SHALL parse and evaluate DynamoDB expressions using a hand-written recursive descent parser.

## Requirements

### Requirement: Tokenizer
The tokenizer SHALL produce tokens from expression strings.

#### Scenario: Tokenize comparison
- **GIVEN** expression "price > :minPrice"
- **THEN** tokens SHALL be [IDENTIFIER("price"), GT, PLACEHOLDER(":minPrice")]

#### Scenario: Tokenize document path
- **GIVEN** expression "user.address.city"
- **THEN** tokens SHALL be [IDENTIFIER("user"), DOT, IDENTIFIER("address"), DOT, IDENTIFIER("city")]

#### Scenario: Tokenize list index
- **GIVEN** expression "items[0].name"
- **THEN** tokens SHALL be [IDENTIFIER("items"), LBRACKET, NUMBER(0), RBRACKET, DOT, IDENTIFIER("name")]

### Requirement: ExpressionAttributeNames Resolution
The server SHALL substitute #name placeholders with actual attribute names.

#### Scenario: Name substitution
- **GIVEN** ExpressionAttributeNames {"#s": "status"}
- **AND** expression "#s = :val"
- **THEN** #s SHALL resolve to attribute name "status"

#### Scenario: Reserved word handling
- **GIVEN** attribute named "status" (a reserved word)
- **WHEN** used via #s in ExpressionAttributeNames
- **THEN** it SHALL resolve correctly

### Requirement: ExpressionAttributeValues Resolution
The server SHALL substitute :name placeholders with provided attribute values.

#### Scenario: Value substitution
- **GIVEN** ExpressionAttributeValues {":val": {"S": "active"}}
- **THEN** :val SHALL resolve to {"S": "active"}

### Requirement: Document Path Evaluation
The server SHALL navigate nested attributes via dot notation and list indexes.

#### Scenario: Nested map access
- **GIVEN** item {"user": {"M": {"name": {"S": "Alice"}}}}
- **WHEN** path is "user.name"
- **THEN** resolved value SHALL be {"S": "Alice"}

#### Scenario: List index access
- **GIVEN** item {"items": {"L": [{"S": "a"}, {"S": "b"}]}}
- **WHEN** path is "items[1]"
- **THEN** resolved value SHALL be {"S": "b"}

#### Scenario: Mixed nesting
- **GIVEN** deeply nested item
- **WHEN** path is "orders[0].items[2].name"
- **THEN** the server SHALL traverse maps and lists correctly

### Requirement: ProjectionExpression
The server SHALL return only the attributes specified in ProjectionExpression.

#### Scenario: Simple projection
- **GIVEN** item with attributes pk, name, age, email
- **WHEN** ProjectionExpression is "pk, name"
- **THEN** the returned item SHALL contain only pk and name

#### Scenario: Nested projection
- **WHEN** ProjectionExpression is "pk, user.name"
- **THEN** the returned item SHALL contain pk and the nested name within user
