# Expression Engine Specification

## Purpose
The server SHALL parse and evaluate DynamoDB expressions using ANTLR4 grammar files and generated parsers.

## Requirements

### Requirement: Grammar-Based Parsing
The server SHALL use ANTLR4 grammar files (.g4) to define the lexical and syntactic rules for DynamoDB expressions. The ANTLR-generated lexer and parser SHALL tokenize and parse expression strings into parse trees, which are then converted to AST nodes via visitor classes.

#### Scenario: Parse comparison
- **GIVEN** expression "price > :minPrice"
- **THEN** the ANTLR parser SHALL produce a parse tree that the visitor converts to a ComparisonNode with a PathNode("price"), operator ">", and ValuePlaceholderNode(":minPrice")

#### Scenario: Parse document path
- **GIVEN** expression "user.address.city"
- **THEN** the ANTLR parser SHALL produce a documentPath rule match that the visitor converts to a PathNode with a DocumentPath containing elements ["user", "address", "city"]

#### Scenario: Parse list index
- **GIVEN** expression "items[0].name"
- **THEN** the ANTLR parser SHALL produce a documentPath rule match that the visitor converts to a PathNode with a DocumentPath containing elements [Attribute("items"), Index(0), Attribute("name")]

### Requirement: Condition Expression Grammar
The server SHALL provide a DynamoDbCondition.g4 grammar that supports the full condition expression syntax used by ConditionExpression, FilterExpression, and KeyConditionExpression.

#### Scenario: Logical operators
- **GIVEN** expression "price > :min AND status = :active"
- **THEN** the parser SHALL produce a LogicalNode with operator "AND" containing two ComparisonNodes

#### Scenario: NOT operator
- **GIVEN** expression "NOT contains(tags, :val)"
- **THEN** the parser SHALL produce a NotNode wrapping a FunctionNode

#### Scenario: BETWEEN
- **GIVEN** expression "age BETWEEN :low AND :high"
- **THEN** the parser SHALL produce a BetweenNode

#### Scenario: IN
- **GIVEN** expression "status IN (:s1, :s2, :s3)"
- **THEN** the parser SHALL produce an InNode with three value placeholders

#### Scenario: Parenthesized grouping
- **GIVEN** expression "(a = :v1 OR b = :v2) AND c = :v3"
- **THEN** the parser SHALL respect parentheses for precedence

#### Scenario: Functions
- **GIVEN** expressions using attribute_exists, attribute_not_exists, attribute_type, begins_with, contains, size
- **THEN** the parser SHALL produce FunctionNode instances with correct argument lists

### Requirement: Update Expression Grammar
The server SHALL provide a DynamoDbUpdate.g4 grammar that supports the full update expression syntax.

#### Scenario: SET with value
- **GIVEN** expression "SET #s = :val"
- **THEN** the parser SHALL produce an UpdateAction with type "SET"

#### Scenario: SET with arithmetic
- **GIVEN** expression "SET count = count + :inc"
- **THEN** the parser SHALL produce an UpdateAction with an ArithmeticNode value

#### Scenario: SET with function
- **GIVEN** expression "SET val = if_not_exists(val, :default)"
- **THEN** the parser SHALL produce an UpdateAction with a FunctionNode value

#### Scenario: REMOVE
- **GIVEN** expression "REMOVE attr1, attr2.nested"
- **THEN** the parser SHALL produce UpdateActions with type "REMOVE"

#### Scenario: ADD and DELETE
- **GIVEN** expression "ADD counter :inc DELETE tags :removals"
- **THEN** the parser SHALL produce UpdateActions with types "ADD" and "DELETE"

#### Scenario: Combined clauses
- **GIVEN** expression "SET a = :v1 REMOVE b ADD c :v2"
- **THEN** the parser SHALL produce UpdateActions for all three clause types

### Requirement: ExpressionAttributeNames Resolution
The server SHALL substitute #name placeholders with actual attribute names during AST construction in the ANTLR visitor.

#### Scenario: Name substitution
- **GIVEN** ExpressionAttributeNames {"#s": "status"}
- **AND** expression "#s = :val"
- **THEN** #s SHALL resolve to attribute name "status"

#### Scenario: Reserved word handling
- **GIVEN** attribute named "status" (a reserved word)
- **WHEN** used via #s in ExpressionAttributeNames
- **THEN** it SHALL resolve correctly

### Requirement: ExpressionAttributeValues Resolution
The server SHALL substitute :name placeholders with provided attribute values during evaluation.

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

### Requirement: Error Handling
The server SHALL report parse errors as ValidationException with position information.

#### Scenario: Malformed expression
- **GIVEN** expression "price >> :val"
- **THEN** the server SHALL throw a ValidationException indicating the error position

#### Scenario: Unexpected token
- **GIVEN** expression "AND price = :val"
- **THEN** the server SHALL throw a ValidationException for the unexpected leading AND
