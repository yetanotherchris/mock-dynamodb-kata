# ANTLR Expression Engine Proposal

## Problem
The expression engine currently uses a hand-written recursive descent tokenizer and parser (Token.cs, Tokenizer.cs, ConditionExpressionParser.cs, UpdateExpressionParser.cs). While functional, this approach has drawbacks:

- The grammar is implicit in the code rather than declared as a formal specification
- Adding new expression syntax requires modifying multiple parser methods and token types
- No separation between grammar definition and parsing logic
- Error messages are manually constructed and inconsistent
- The tokenizer and parser are tightly coupled (e.g. DocumentPath.Parse takes raw token lists and position references)

## Solution
Replace the hand-written tokenizer and parsers with ANTLR4 grammar files and generated lexer/parser code. Use ANTLR visitor classes to construct the existing AST node types (ExpressionNode hierarchy), keeping the evaluation layer (ConditionEvaluator, UpdateEvaluator) unchanged.

This gives us:
- A formal, readable grammar specification for DynamoDB expressions
- Auto-generated lexer and parser with proper error reporting
- Clean separation between grammar definition, parse tree construction, and AST building
- Easier maintenance when adding new expression features

## Scope
- ANTLR4 grammar file for condition/filter/key-condition expressions (DynamoDbCondition.g4)
- ANTLR4 grammar file for update expressions (DynamoDbUpdate.g4)
- ANTLR visitor implementations that build existing ExpressionNode / UpdateAction types
- NuGet package references for Antlr4.Runtime.Standard and Antlr4.CodeGenerator
- Removal of Token.cs, Tokenizer.cs, ConditionExpressionParser.cs, UpdateExpressionParser.cs
- Unit tests for the ANTLR grammars and visitors
- All existing integration tests must continue to pass

## Out of Scope
- Changes to the AST node types (ExpressionAst.cs) — these remain as-is
- Changes to the evaluators (ConditionEvaluator.cs, UpdateEvaluator.cs) — these remain as-is
- Changes to DocumentPath resolution logic (DocumentPath.cs) — resolution stays, but parsing moves to ANTLR
- ProjectionExpression parsing (currently handled inline in QueryScanOperations) — may be added to a grammar in a follow-up
- PartiQL support
