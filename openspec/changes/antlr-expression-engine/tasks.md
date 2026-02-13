# Tasks

## Phase 1: Project Setup
- [ ] Add Antlr4.Runtime.Standard NuGet package to MockDynamoDB.Core.csproj
- [ ] Add Antlr4BuildTasks (or Antlr4.CodeGenerator) NuGet package for build-time .g4 compilation
- [ ] Create `src/MockDynamoDB.Core/Expressions/Grammar/` directory
- [ ] Verify `dotnet build` succeeds with ANTLR tooling installed

## Phase 2: Condition Expression Grammar
- [ ] Write `DynamoDbCondition.g4` with lexer rules (operators, placeholders, identifiers, keywords)
- [ ] Write parser rules for condition expressions (OR, AND, NOT, comparison, BETWEEN, IN, functions, parentheses)
- [ ] Verify grammar compiles and generates C# lexer/parser/visitor base classes

## Phase 3: Condition Expression Visitor
- [ ] Implement `ConditionExpressionVisitor` extending the generated base visitor
- [ ] Map orExpression/andExpression rules to `LogicalNode`
- [ ] Map notExpression rule to `NotNode`
- [ ] Map comparison rule to `ComparisonNode`
- [ ] Map BETWEEN rule to `BetweenNode`
- [ ] Map IN rule to `InNode`
- [ ] Map function rule to `FunctionNode`
- [ ] Map documentPath rule to `PathNode` with `DocumentPath` construction
- [ ] Map valuePlaceholder rule to `ValuePlaceholderNode`
- [ ] Handle `ExpressionAttributeNames` resolution (#name substitution) in path building
- [ ] Add `DynamoDbExpressionParser.ParseCondition()` static entry point

## Phase 4: Update Expression Grammar
- [ ] Write `DynamoDbUpdate.g4` with lexer rules (SET, REMOVE, ADD, DELETE keywords, shared tokens)
- [ ] Write parser rules for update expressions (set/remove/add/delete clauses, arithmetic, functions)
- [ ] Verify grammar compiles and generates C# lexer/parser/visitor base classes

## Phase 5: Update Expression Visitor
- [ ] Implement `UpdateExpressionVisitor` extending the generated base visitor
- [ ] Map setClause/setAction rules to `UpdateAction` with type "SET"
- [ ] Map SET value with arithmetic (+/-) to `ArithmeticNode`
- [ ] Map SET functions (if_not_exists, list_append) to `FunctionNode`
- [ ] Map removeClause to `UpdateAction` with type "REMOVE"
- [ ] Map addClause/addAction to `UpdateAction` with type "ADD"
- [ ] Map deleteClause/deleteAction to `UpdateAction` with type "DELETE"
- [ ] Handle `ExpressionAttributeNames` resolution in path building
- [ ] Add `DynamoDbExpressionParser.ParseUpdate()` static entry point

## Phase 6: Error Handling
- [ ] Implement custom `IANTLRErrorListener` that throws `ValidationException` with position info
- [ ] Attach error listener to both condition and update parsers
- [ ] Verify error messages include token position matching existing format

## Phase 7: DocumentPath Refactor
- [ ] Add factory method or constructor on `DocumentPath` that accepts a list of path elements directly (for use by visitors)
- [ ] Remove `DocumentPath.Parse(List<Token>, ref int, ...)` method that takes raw token lists
- [ ] Verify path resolution and mutation logic (Resolve, SetValue, Remove) remains unchanged

## Phase 8: Wire Up and Remove Old Code
- [ ] Update `ItemOperations.cs` to use `DynamoDbExpressionParser.ParseCondition()` instead of `ConditionExpressionParser`
- [ ] Update `QueryScanOperations.cs` to use `DynamoDbExpressionParser.ParseCondition()` for KeyConditionExpression and FilterExpression
- [ ] Update `QueryScanOperations.cs` / `BatchOperations.cs` for any other condition parsing call sites
- [ ] Update `ItemOperations.cs` to use `DynamoDbExpressionParser.ParseUpdate()` instead of `UpdateExpressionParser`
- [ ] Update `TransactionOperations.cs` to use new parser entry points
- [ ] Delete `Token.cs`
- [ ] Delete `Tokenizer.cs`
- [ ] Delete `ConditionExpressionParser.cs`
- [ ] Delete `UpdateExpressionParser.cs`

## Phase 9: Unit Tests
- [ ] Add unit tests for condition grammar: simple comparison (e.g. `price > :min`)
- [ ] Add unit tests for condition grammar: logical operators (AND, OR, NOT)
- [ ] Add unit tests for condition grammar: BETWEEN and IN
- [ ] Add unit tests for condition grammar: all six function types
- [ ] Add unit tests for condition grammar: nested parenthesized expressions
- [ ] Add unit tests for condition grammar: document paths with dots and indexes
- [ ] Add unit tests for update grammar: SET with simple value, arithmetic, if_not_exists, list_append
- [ ] Add unit tests for update grammar: REMOVE single and multiple paths
- [ ] Add unit tests for update grammar: ADD and DELETE actions
- [ ] Add unit tests for update grammar: combined clauses (SET + REMOVE in one expression)
- [ ] Add unit tests for error cases: malformed expressions produce ValidationException
- [ ] Add unit tests for ExpressionAttributeNames resolution through the ANTLR path

## Phase 10: Integration Test Verification
- [ ] Run all existing spec tests (`dotnet test`) and verify they pass
- [ ] Verify query with sort key conditions (begins_with, BETWEEN)
- [ ] Verify update with arithmetic and condition expressions
- [ ] Verify transaction operations with condition checks
- [ ] Verify batch operations
