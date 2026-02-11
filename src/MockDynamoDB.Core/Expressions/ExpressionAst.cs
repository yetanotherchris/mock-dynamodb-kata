namespace MockDynamoDB.Core.Expressions;

public abstract class ExpressionNode { }

// Comparison: path op value, value op path, etc.
public class ComparisonNode : ExpressionNode
{
    public ExpressionNode Left { get; }
    public string Operator { get; }
    public ExpressionNode Right { get; }

    public ComparisonNode(ExpressionNode left, string op, ExpressionNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

// AND / OR
public class LogicalNode : ExpressionNode
{
    public ExpressionNode Left { get; }
    public string Operator { get; } // AND, OR
    public ExpressionNode Right { get; }

    public LogicalNode(ExpressionNode left, string op, ExpressionNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

// NOT
public class NotNode : ExpressionNode
{
    public ExpressionNode Operand { get; }
    public NotNode(ExpressionNode operand) { Operand = operand; }
}

// BETWEEN
public class BetweenNode : ExpressionNode
{
    public ExpressionNode Value { get; }
    public ExpressionNode Low { get; }
    public ExpressionNode High { get; }

    public BetweenNode(ExpressionNode value, ExpressionNode low, ExpressionNode high)
    {
        Value = value;
        Low = low;
        High = high;
    }
}

// IN
public class InNode : ExpressionNode
{
    public ExpressionNode Value { get; }
    public List<ExpressionNode> List { get; }

    public InNode(ExpressionNode value, List<ExpressionNode> list)
    {
        Value = value;
        List = list;
    }
}

// Function call: attribute_exists, begins_with, contains, size, etc.
public class FunctionNode : ExpressionNode
{
    public string FunctionName { get; }
    public List<ExpressionNode> Arguments { get; }

    public FunctionNode(string functionName, List<ExpressionNode> arguments)
    {
        FunctionName = functionName;
        Arguments = arguments;
    }
}

// Document path reference
public class PathNode : ExpressionNode
{
    public DocumentPath Path { get; }
    public PathNode(DocumentPath path) { Path = path; }
}

// Value placeholder (:val)
public class ValuePlaceholderNode : ExpressionNode
{
    public string Placeholder { get; }
    public ValuePlaceholderNode(string placeholder) { Placeholder = placeholder; }
}

// Arithmetic: path + :val, path - :val
public class ArithmeticNode : ExpressionNode
{
    public ExpressionNode Left { get; }
    public string Operator { get; } // +, -
    public ExpressionNode Right { get; }

    public ArithmeticNode(ExpressionNode left, string op, ExpressionNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}
