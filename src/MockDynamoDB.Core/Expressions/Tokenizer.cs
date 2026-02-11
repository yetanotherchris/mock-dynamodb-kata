namespace MockDynamoDB.Core.Expressions;

public class Tokenizer
{
    private readonly string _input;
    private int _pos;

    public Tokenizer(string input)
    {
        _input = input;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _input.Length)
        {
            SkipWhitespace();
            if (_pos >= _input.Length)
                break;

            var ch = _input[_pos];
            var startPos = _pos;

            switch (ch)
            {
                case '.':
                    tokens.Add(new Token(TokenType.Dot, ".", _pos++));
                    break;
                case '[':
                    tokens.Add(new Token(TokenType.LeftBracket, "[", _pos++));
                    break;
                case ']':
                    tokens.Add(new Token(TokenType.RightBracket, "]", _pos++));
                    break;
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "(", _pos++));
                    break;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")", _pos++));
                    break;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ",", _pos++));
                    break;
                case '+':
                    tokens.Add(new Token(TokenType.Plus, "+", _pos++));
                    break;
                case '-':
                    tokens.Add(new Token(TokenType.Minus, "-", _pos++));
                    break;
                case '=':
                    tokens.Add(new Token(TokenType.Equals, "=", _pos++));
                    break;
                case '<':
                    _pos++;
                    if (_pos < _input.Length && _input[_pos] == '>')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.NotEquals, "<>", startPos));
                    }
                    else if (_pos < _input.Length && _input[_pos] == '=')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.LessThanOrEqual, "<=", startPos));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.LessThan, "<", startPos));
                    }
                    break;
                case '>':
                    _pos++;
                    if (_pos < _input.Length && _input[_pos] == '=')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenType.GreaterThanOrEqual, ">=", startPos));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.GreaterThan, ">", startPos));
                    }
                    break;
                case ':':
                    tokens.Add(ReadPlaceholder());
                    break;
                case '#':
                    tokens.Add(ReadNamePlaceholder());
                    break;
                default:
                    if (char.IsDigit(ch))
                    {
                        tokens.Add(ReadNumber());
                    }
                    else if (char.IsLetter(ch) || ch == '_')
                    {
                        tokens.Add(ReadIdentifier());
                    }
                    else
                    {
                        throw new Models.ValidationException(
                            $"Invalid character '{ch}' at position {_pos} in expression");
                    }
                    break;
            }
        }

        tokens.Add(new Token(TokenType.EOF, "", _pos));
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
            _pos++;
    }

    private Token ReadPlaceholder()
    {
        var start = _pos;
        _pos++; // skip :
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
            _pos++;
        return new Token(TokenType.Placeholder, _input[start.._pos], start);
    }

    private Token ReadNamePlaceholder()
    {
        var start = _pos;
        _pos++; // skip #
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
            _pos++;
        return new Token(TokenType.NamePlaceholder, _input[start.._pos], start);
    }

    private Token ReadNumber()
    {
        var start = _pos;
        while (_pos < _input.Length && char.IsDigit(_input[_pos]))
            _pos++;
        return new Token(TokenType.Number, _input[start.._pos], start);
    }

    private Token ReadIdentifier()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
            _pos++;
        return new Token(TokenType.Identifier, _input[start.._pos], start);
    }
}
