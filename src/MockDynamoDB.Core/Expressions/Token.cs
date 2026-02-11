namespace MockDynamoDB.Core.Expressions;

public enum TokenType
{
    Identifier,        // attribute name or keyword
    Placeholder,       // :value placeholder
    NamePlaceholder,   // #name placeholder
    Number,            // numeric literal (for list indexes)
    StringLiteral,     // 'string' in expressions (not common in DynamoDB but for completeness)
    Dot,               // .
    LeftBracket,       // [
    RightBracket,      // ]
    LeftParen,         // (
    RightParen,        // )
    Comma,             // ,
    Equals,            // =
    NotEquals,         // <>
    LessThan,          // <
    LessThanOrEqual,   // <=
    GreaterThan,       // >
    GreaterThanOrEqual,// >=
    Plus,              // +
    Minus,             // -
    EOF
}

public class Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Position { get; }

    public Token(TokenType type, string value, int position)
    {
        Type = type;
        Value = value;
        Position = position;
    }

    public override string ToString() => $"{Type}({Value}) @{Position}";
}
