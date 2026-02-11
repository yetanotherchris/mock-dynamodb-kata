using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class ConditionExpressionParser
{
    private readonly List<Token> _tokens;
    private readonly Dictionary<string, string>? _expressionAttributeNames;
    private int _pos;

    public ConditionExpressionParser(string expression, Dictionary<string, string>? expressionAttributeNames = null)
    {
        _expressionAttributeNames = expressionAttributeNames;
        var tokenizer = new Tokenizer(expression);
        _tokens = tokenizer.Tokenize();
        _pos = 0;
    }

    public ExpressionNode Parse()
    {
        var node = ParseOr();
        if (_tokens[_pos].Type != TokenType.EOF)
            throw new ValidationException($"Unexpected token '{_tokens[_pos].Value}' at position {_tokens[_pos].Position}");
        return node;
    }

    private ExpressionNode ParseOr()
    {
        var left = ParseAnd();

        while (CurrentIs(TokenType.Identifier, "OR"))
        {
            _pos++;
            var right = ParseAnd();
            left = new LogicalNode(left, "OR", right);
        }

        return left;
    }

    private ExpressionNode ParseAnd()
    {
        var left = ParseNot();

        while (CurrentIs(TokenType.Identifier, "AND"))
        {
            _pos++;
            var right = ParseNot();
            left = new LogicalNode(left, "AND", right);
        }

        return left;
    }

    private ExpressionNode ParseNot()
    {
        if (CurrentIs(TokenType.Identifier, "NOT"))
        {
            _pos++;
            var operand = ParseNot();
            return new NotNode(operand);
        }

        return ParseComparison();
    }

    private ExpressionNode ParseComparison()
    {
        // Check for function calls first
        if (IsFunction())
        {
            return ParseFunction();
        }

        // Parenthesized expression
        if (Current().Type == TokenType.LeftParen)
        {
            _pos++;
            var inner = ParseOr();
            Expect(TokenType.RightParen);
            return inner;
        }

        // operand (comparison | BETWEEN | IN)
        var left = ParseOperand();

        // BETWEEN
        if (CurrentIs(TokenType.Identifier, "BETWEEN"))
        {
            _pos++;
            var low = ParseOperand();
            ExpectIdentifier("AND");
            var high = ParseOperand();
            return new BetweenNode(left, low, high);
        }

        // IN
        if (CurrentIs(TokenType.Identifier, "IN"))
        {
            _pos++;
            Expect(TokenType.LeftParen);
            var list = new List<ExpressionNode>();
            list.Add(ParseOperand());
            while (Current().Type == TokenType.Comma)
            {
                _pos++;
                list.Add(ParseOperand());
            }
            Expect(TokenType.RightParen);
            return new InNode(left, list);
        }

        // Comparison operators
        var op = Current().Type switch
        {
            TokenType.Equals => "=",
            TokenType.NotEquals => "<>",
            TokenType.LessThan => "<",
            TokenType.LessThanOrEqual => "<=",
            TokenType.GreaterThan => ">",
            TokenType.GreaterThanOrEqual => ">=",
            _ => null
        };

        if (op != null)
        {
            _pos++;
            var right = ParseOperand();
            return new ComparisonNode(left, op, right);
        }

        // If we got a function-like node that stands alone (attribute_exists etc.)
        // it was already handled above
        return left;
    }

    private ExpressionNode ParseOperand()
    {
        if (IsFunction())
            return ParseFunction();

        var token = Current();

        if (token.Type == TokenType.Placeholder)
        {
            _pos++;
            return new ValuePlaceholderNode(token.Value);
        }

        if (token.Type == TokenType.Identifier || token.Type == TokenType.NamePlaceholder)
        {
            return new PathNode(DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames));
        }

        throw new ValidationException($"Unexpected token '{token.Value}' at position {token.Position}");
    }

    private ExpressionNode ParseFunction()
    {
        var name = Current().Value;
        _pos++;
        Expect(TokenType.LeftParen);

        var args = new List<ExpressionNode>();
        if (Current().Type != TokenType.RightParen)
        {
            args.Add(ParseFunctionArg());
            while (Current().Type == TokenType.Comma)
            {
                _pos++;
                args.Add(ParseFunctionArg());
            }
        }

        Expect(TokenType.RightParen);
        return new FunctionNode(name, args);
    }

    private ExpressionNode ParseFunctionArg()
    {
        if (Current().Type == TokenType.Placeholder)
        {
            var token = Current();
            _pos++;
            return new ValuePlaceholderNode(token.Value);
        }

        if (Current().Type == TokenType.Identifier || Current().Type == TokenType.NamePlaceholder)
        {
            // Could be a function or a path
            if (IsFunction())
                return ParseFunction();
            return new PathNode(DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames));
        }

        throw new ValidationException($"Unexpected token in function argument: '{Current().Value}'");
    }

    private bool IsFunction()
    {
        if (Current().Type != TokenType.Identifier) return false;
        var name = Current().Value;
        if (_pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.LeftParen)
        {
            return name is "attribute_exists" or "attribute_not_exists" or "attribute_type"
                or "begins_with" or "contains" or "size";
        }
        return false;
    }

    private bool CurrentIs(TokenType type, string? value = null)
    {
        var token = Current();
        return token.Type == type && (value == null || string.Equals(token.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private Token Current() => _tokens[_pos];

    private void Expect(TokenType type)
    {
        if (Current().Type != type)
            throw new ValidationException($"Expected {type} at position {Current().Position}, got {Current().Type}");
        _pos++;
    }

    private void ExpectIdentifier(string value)
    {
        if (!CurrentIs(TokenType.Identifier, value))
            throw new ValidationException($"Expected '{value}' at position {Current().Position}");
        _pos++;
    }
}
