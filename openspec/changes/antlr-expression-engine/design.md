# Design

## Architecture

The change replaces the hand-written lexer/parser layer while preserving the AST and evaluation layers:

```
Before:  Expression string → Tokenizer → Token list → Hand-written Parser → AST nodes → Evaluator
After:   Expression string → ANTLR Lexer → ANTLR Parser → Parse Tree → Visitor → AST nodes → Evaluator
```

## Grammar Files

Two ANTLR4 combined grammar files in `src/MockDynamoDB.Core/Expressions/Grammar/`:

### DynamoDbCondition.g4
Covers ConditionExpression, FilterExpression, and KeyConditionExpression.

```
condition        → orExpression EOF
orExpression     → andExpression (OR andExpression)*
andExpression    → notExpression (AND notExpression)*
notExpression    → NOT notExpression | comparison
comparison       → operand comparator operand
                 | operand BETWEEN operand AND operand
                 | operand IN '(' operand (',' operand)* ')'
                 | function
                 | '(' orExpression ')'
comparator       → '=' | '<>' | '<' | '<=' | '>' | '>='
operand          → documentPath | valuePlaceholder | function
documentPath     → pathElement ('.' pathElement | '[' NUMBER ']')*
pathElement      → IDENTIFIER | NAME_PLACEHOLDER
valuePlaceholder → PLACEHOLDER
function         → functionName '(' operand (',' operand)* ')'
functionName     → 'attribute_exists' | 'attribute_not_exists' | 'attribute_type'
                 | 'begins_with' | 'contains' | 'size'
```

### DynamoDbUpdate.g4
Covers UpdateExpression with SET, REMOVE, ADD, DELETE clauses.

```
updateExpression → clause+
clause           → setClause | removeClause | addClause | deleteClause
setClause        → SET setAction (',' setAction)*
setAction        → documentPath '=' setValue
setValue          → operand ('+' | '-') operand | operand
removeClause     → REMOVE documentPath (',' documentPath)*
addClause        → ADD addAction (',' addAction)*
addAction        → documentPath operand
deleteClause     → DELETE deleteAction (',' deleteAction)*
deleteAction     → documentPath operand
operand          → documentPath | valuePlaceholder | function
function         → functionName '(' operand (',' operand)* ')'
functionName     → 'if_not_exists' | 'list_append'
```

### Shared Lexer Rules
Both grammars share similar lexer rules for:
- `PLACEHOLDER`: `:` followed by identifier characters
- `NAME_PLACEHOLDER`: `#` followed by identifier characters
- `IDENTIFIER`: letter/underscore followed by alphanumeric/underscore
- `NUMBER`: digit sequence
- Operators and punctuation
- Case-insensitive keywords: `AND`, `OR`, `NOT`, `BETWEEN`, `IN`, `SET`, `REMOVE`, `ADD`, `DELETE`
- Whitespace (skip channel)

## Visitor Pattern

Two ANTLR visitor classes convert parse trees to the existing AST:

### ConditionExpressionVisitor
- Visits parse tree, returns `ExpressionNode` hierarchy
- Maps grammar rules to existing node types: `ComparisonNode`, `LogicalNode`, `NotNode`, `BetweenNode`, `InNode`, `FunctionNode`, `PathNode`, `ValuePlaceholderNode`
- Resolves `ExpressionAttributeNames` (#name → real attribute name) during document path construction
- Constructs `DocumentPath` instances from the parse tree path elements

### UpdateExpressionVisitor
- Visits parse tree, returns `List<UpdateAction>`
- Maps SET/REMOVE/ADD/DELETE clauses to `UpdateAction` objects
- Constructs `ArithmeticNode`, `FunctionNode`, `PathNode`, `ValuePlaceholderNode` for SET values
- Resolves `ExpressionAttributeNames` during path construction

## NuGet Dependencies

Add to `MockDynamoDB.Core.csproj`:
- `Antlr4.Runtime.Standard` (runtime library, ~4.13+)
- `Antlr4BuildTasks` or `Antlr4.CodeGenerator` (build-time code generation from .g4 files)

The .g4 files are compiled at build time into C# lexer/parser/visitor base classes.

## Files Removed
- `Token.cs` — replaced by ANTLR token types
- `Tokenizer.cs` — replaced by ANTLR-generated lexer
- `ConditionExpressionParser.cs` — replaced by ANTLR-generated parser + visitor
- `UpdateExpressionParser.cs` — replaced by ANTLR-generated parser + visitor

## Files Preserved (Unchanged)
- `ExpressionAst.cs` — AST node types used by visitors and evaluators
- `DocumentPath.cs` — path resolution and mutation logic (parsing portion moves to visitors)
- `ConditionEvaluator.cs` — evaluates condition AST against items
- `UpdateEvaluator.cs` — applies update actions to items

## Files Modified
- `DocumentPath.cs` — remove the `Parse(List<Token>, ref int, ...)` method; add a constructor or factory that accepts path elements directly (used by visitors)
- `MockDynamoDB.Core.csproj` — add ANTLR NuGet packages
- Operation files (`ItemOperations.cs`, `QueryScanOperations.cs`, etc.) — update parser instantiation to use new ANTLR-based entry points

## Entry Point API

Provide static factory methods to keep the calling code simple:

```csharp
// Condition/Filter/KeyCondition parsing
public static class DynamoDbExpressionParser
{
    public static ExpressionNode ParseCondition(
        string expression,
        Dictionary<string, string>? expressionAttributeNames = null);

    public static List<UpdateAction> ParseUpdate(
        string expression,
        Dictionary<string, string>? expressionAttributeNames = null);
}
```

These methods internally create the ANTLR lexer, parser, and visitor, then return the AST. Callers in the operation classes use these instead of instantiating parsers directly.

## Error Handling

- ANTLR's default error strategy throws `RecognitionException` on syntax errors
- Add a custom `IANTLRErrorListener` that catches parse errors and throws `ValidationException` with position information matching the existing error format
- This preserves backward compatibility for callers that catch `ValidationException`
