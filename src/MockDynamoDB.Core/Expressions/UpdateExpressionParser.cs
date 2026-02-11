using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class UpdateAction
{
    public string Type { get; set; } = ""; // SET, REMOVE, ADD, DELETE
    public DocumentPath Path { get; set; } = null!;
    public ExpressionNode? Value { get; set; }
}

public class UpdateExpressionParser
{
    private readonly List<Token> _tokens;
    private readonly Dictionary<string, string>? _expressionAttributeNames;
    private int _pos;

    public UpdateExpressionParser(string expression, Dictionary<string, string>? expressionAttributeNames = null)
    {
        _expressionAttributeNames = expressionAttributeNames;
        var tokenizer = new Tokenizer(expression);
        _tokens = tokenizer.Tokenize();
        _pos = 0;
    }

    public List<UpdateAction> Parse()
    {
        var actions = new List<UpdateAction>();

        while (_tokens[_pos].Type != TokenType.EOF)
        {
            var clause = _tokens[_pos].Value.ToUpperInvariant();

            switch (clause)
            {
                case "SET":
                    _pos++;
                    ParseSetActions(actions);
                    break;
                case "REMOVE":
                    _pos++;
                    ParseRemoveActions(actions);
                    break;
                case "ADD":
                    _pos++;
                    ParseAddActions(actions);
                    break;
                case "DELETE":
                    _pos++;
                    ParseDeleteActions(actions);
                    break;
                default:
                    throw new ValidationException($"Invalid UpdateExpression: unexpected token '{_tokens[_pos].Value}'");
            }
        }

        return actions;
    }

    private void ParseSetActions(List<UpdateAction> actions)
    {
        do
        {
            var path = DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames);
            Expect(TokenType.Equals);
            var value = ParseSetValue();
            actions.Add(new UpdateAction { Type = "SET", Path = path, Value = value });
        } while (TryConsume(TokenType.Comma));
    }

    private ExpressionNode ParseSetValue()
    {
        var left = ParseSetPrimary();

        // Check for arithmetic: left + right, left - right
        if (Current().Type == TokenType.Plus || Current().Type == TokenType.Minus)
        {
            var op = Current().Value;
            _pos++;
            var right = ParseSetPrimary();
            return new ArithmeticNode(left, op, right);
        }

        return left;
    }

    private ExpressionNode ParseSetPrimary()
    {
        // Function call: if_not_exists(...), list_append(...)
        if (Current().Type == TokenType.Identifier && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.LeftParen)
        {
            var name = Current().Value;
            _pos++;
            Expect(TokenType.LeftParen);

            var args = new List<ExpressionNode>();
            args.Add(ParseSetArg());
            while (Current().Type == TokenType.Comma)
            {
                _pos++;
                args.Add(ParseSetArg());
            }
            Expect(TokenType.RightParen);
            return new FunctionNode(name, args);
        }

        // Value placeholder
        if (Current().Type == TokenType.Placeholder)
        {
            var token = Current();
            _pos++;
            return new ValuePlaceholderNode(token.Value);
        }

        // Document path
        if (Current().Type == TokenType.Identifier || Current().Type == TokenType.NamePlaceholder)
        {
            return new PathNode(DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames));
        }

        throw new ValidationException($"Unexpected token in SET value: '{Current().Value}'");
    }

    private ExpressionNode ParseSetArg()
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
            if (Current().Type == TokenType.Identifier && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.LeftParen)
            {
                var name = Current().Value;
                _pos++;
                Expect(TokenType.LeftParen);
                var args = new List<ExpressionNode>();
                args.Add(ParseSetArg());
                while (Current().Type == TokenType.Comma)
                {
                    _pos++;
                    args.Add(ParseSetArg());
                }
                Expect(TokenType.RightParen);
                return new FunctionNode(name, args);
            }
            return new PathNode(DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames));
        }

        throw new ValidationException($"Unexpected token in function argument: '{Current().Value}'");
    }

    private void ParseRemoveActions(List<UpdateAction> actions)
    {
        do
        {
            var path = DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames);
            actions.Add(new UpdateAction { Type = "REMOVE", Path = path });
        } while (TryConsume(TokenType.Comma));
    }

    private void ParseAddActions(List<UpdateAction> actions)
    {
        do
        {
            var path = DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames);
            var value = ParseOperand();
            actions.Add(new UpdateAction { Type = "ADD", Path = path, Value = value });
        } while (TryConsume(TokenType.Comma));
    }

    private void ParseDeleteActions(List<UpdateAction> actions)
    {
        do
        {
            var path = DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames);
            var value = ParseOperand();
            actions.Add(new UpdateAction { Type = "DELETE", Path = path, Value = value });
        } while (TryConsume(TokenType.Comma));
    }

    private ExpressionNode ParseOperand()
    {
        if (Current().Type == TokenType.Placeholder)
        {
            var token = Current();
            _pos++;
            return new ValuePlaceholderNode(token.Value);
        }

        if (Current().Type == TokenType.Identifier || Current().Type == TokenType.NamePlaceholder)
        {
            return new PathNode(DocumentPath.Parse(_tokens, ref _pos, _expressionAttributeNames));
        }

        throw new ValidationException($"Unexpected token: '{Current().Value}'");
    }

    private Token Current() => _tokens[_pos];

    private void Expect(TokenType type)
    {
        if (Current().Type != type)
            throw new ValidationException($"Expected {type} at position {Current().Position}, got {Current().Type}");
        _pos++;
    }

    private bool TryConsume(TokenType type)
    {
        if (Current().Type == type)
        {
            _pos++;
            return true;
        }

        // Check if next token is a clause keyword (SET, REMOVE, ADD, DELETE) - don't consume
        if (Current().Type == TokenType.Identifier)
        {
            var upper = Current().Value.ToUpperInvariant();
            if (upper is "SET" or "REMOVE" or "ADD" or "DELETE")
                return false;
        }

        return false;
    }
}
