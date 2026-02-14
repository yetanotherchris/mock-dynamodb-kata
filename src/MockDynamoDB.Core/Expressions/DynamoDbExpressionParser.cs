using Antlr4.Runtime;
using MockDynamoDB.Core.Expressions.Grammar;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public static class DynamoDbExpressionParser
{
    public static ExpressionNode ParseCondition(
        string expression,
        Dictionary<string, string>? expressionAttributeNames = null)
    {
        var input = new AntlrInputStream(expression);
        var lexer = new DynamoDbConditionLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(ThrowingErrorListener.Instance);

        var tokens = new CommonTokenStream(lexer);
        var parser = new DynamoDbConditionParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(ThrowingErrorListener.Instance);

        var tree = parser.condition();
        var visitor = new ConditionExpressionVisitor(expressionAttributeNames);
        return visitor.Visit(tree);
    }

    public static List<UpdateAction> ParseUpdate(
        string expression,
        Dictionary<string, string>? expressionAttributeNames = null)
    {
        var input = new AntlrInputStream(expression);
        var lexer = new DynamoDbUpdateLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(ThrowingErrorListener.Instance);

        var tokens = new CommonTokenStream(lexer);
        var parser = new DynamoDbUpdateParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(ThrowingErrorListener.Instance);

        var tree = parser.updateExpression();
        var visitor = new UpdateExpressionVisitor(expressionAttributeNames);
        visitor.Visit(tree);
        return visitor.GetActions();
    }

    internal class ThrowingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public static readonly ThrowingErrorListener Instance = new();

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new ValidationException(
                $"Invalid expression: {msg} at position {charPositionInLine}");
        }

        public void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            int offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new ValidationException(
                $"Invalid expression: {msg} at position {charPositionInLine}");
        }
    }
}
