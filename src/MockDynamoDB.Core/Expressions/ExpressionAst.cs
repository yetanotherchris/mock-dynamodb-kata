namespace MockDynamoDB.Core.Expressions;

public abstract record ExpressionNode;

// Comparison: path op value, value op path, etc.
public sealed record ComparisonNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;

// AND / OR
public sealed record LogicalNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;

// NOT
public sealed record NotNode(ExpressionNode Operand) : ExpressionNode;

// BETWEEN
public sealed record BetweenNode(ExpressionNode Value, ExpressionNode Low, ExpressionNode High) : ExpressionNode;

// IN
public sealed record InNode(ExpressionNode Value, List<ExpressionNode> List) : ExpressionNode;

// Function call: attribute_exists, begins_with, contains, size, etc.
public sealed record FunctionNode(string FunctionName, List<ExpressionNode> Arguments) : ExpressionNode;

// Document path reference
public sealed record PathNode(DocumentPath Path) : ExpressionNode;

// Value placeholder (:val)
public sealed record ValuePlaceholderNode(string Placeholder) : ExpressionNode;

// Arithmetic: path + :val, path - :val
public sealed record ArithmeticNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;

// Update action for SET, REMOVE, ADD, DELETE clauses
public sealed record UpdateAction
{
    public required string Type { get; init; }
    public required DocumentPath Path { get; init; }
    public ExpressionNode? Value { get; init; }
}
