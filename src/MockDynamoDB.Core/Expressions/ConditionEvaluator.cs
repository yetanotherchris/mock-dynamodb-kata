using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public sealed class ConditionEvaluator(Dictionary<string, AttributeValue>? expressionAttributeValues)
{
    public bool Evaluate(ExpressionNode node, Dictionary<string, AttributeValue> item)
    {
        return node switch
        {
            ComparisonNode comp => EvaluateComparison(comp, item),
            LogicalNode logical => EvaluateLogical(logical, item),
            NotNode not => !Evaluate(not.Operand, item),
            BetweenNode between => EvaluateBetween(between, item),
            InNode inNode => EvaluateIn(inNode, item),
            FunctionNode func => EvaluateFunction(func, item),
            _ => throw new ValidationException($"Unsupported expression node type: {node.GetType().Name}")
        };
    }

    public AttributeValue? ResolveValue(ExpressionNode node, Dictionary<string, AttributeValue> item)
    {
        return node switch
        {
            PathNode path => path.Path.Resolve(item),
            ValuePlaceholderNode placeholder => ResolveValuePlaceholder(placeholder.Placeholder),
            FunctionNode func when func.FunctionName == "size" => EvaluateSize(func, item),
            _ => throw new ValidationException($"Cannot resolve value from {node.GetType().Name}")
        };
    }

    private bool EvaluateComparison(ComparisonNode node, Dictionary<string, AttributeValue> item)
    {
        var left = ResolveValue(node.Left, item);
        var right = ResolveValue(node.Right, item);

        if (left == null || right == null)
            return false;

        var cmp = CompareValues(left, right);
        if (cmp == null) return false;

        return node.Operator switch
        {
            "=" => cmp == 0,
            "<>" => cmp != 0,
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            _ => throw new ValidationException($"Unknown operator: {node.Operator}")
        };
    }

    private bool EvaluateLogical(LogicalNode node, Dictionary<string, AttributeValue> item)
    {
        return node.Operator switch
        {
            "AND" => Evaluate(node.Left, item) && Evaluate(node.Right, item),
            "OR" => Evaluate(node.Left, item) || Evaluate(node.Right, item),
            _ => throw new ValidationException($"Unknown logical operator: {node.Operator}")
        };
    }

    private bool EvaluateBetween(BetweenNode node, Dictionary<string, AttributeValue> item)
    {
        var value = ResolveValue(node.Value, item);
        var low = ResolveValue(node.Low, item);
        var high = ResolveValue(node.High, item);

        if (value == null || low == null || high == null)
            return false;

        var cmpLow = CompareValues(value, low);
        var cmpHigh = CompareValues(value, high);

        return cmpLow != null && cmpHigh != null && cmpLow >= 0 && cmpHigh <= 0;
    }

    private bool EvaluateIn(InNode node, Dictionary<string, AttributeValue> item)
    {
        var value = ResolveValue(node.Value, item);
        if (value == null) return false;

        foreach (var listItem in node.List)
        {
            var listValue = ResolveValue(listItem, item);
            if (listValue != null && CompareValues(value, listValue) == 0)
                return true;
        }

        return false;
    }

    private bool EvaluateFunction(FunctionNode func, Dictionary<string, AttributeValue> item)
    {
        switch (func.FunctionName)
        {
            case "attribute_exists":
            {
                var path = GetPathFromArg(func.Arguments[0]);
                return path.Resolve(item) != null;
            }
            case "attribute_not_exists":
            {
                var path = GetPathFromArg(func.Arguments[0]);
                return path.Resolve(item) == null;
            }
            case "attribute_type":
            {
                var path = GetPathFromArg(func.Arguments[0]);
                var typeValue = ResolveValue(func.Arguments[1], item);
                if (typeValue?.S == null) return false;
                var resolved = path.Resolve(item);
                if (resolved == null) return false;
                return resolved.Type.ToString() == typeValue.S;
            }
            case "begins_with":
            {
                var value = ResolveValue(func.Arguments[0], item);
                var prefix = ResolveValue(func.Arguments[1], item);
                if (value?.S == null || prefix?.S == null) return false;
                return value.S.StartsWith(prefix.S, StringComparison.Ordinal);
            }
            case "contains":
            {
                var container = ResolveValue(func.Arguments[0], item);
                var value = ResolveValue(func.Arguments[1], item);
                if (container == null || value == null) return false;

                if (container.S != null && value.S != null)
                    return container.S.Contains(value.S, StringComparison.Ordinal);
                if (container.SS != null && value.S != null)
                    return container.SS.Contains(value.S);
                if (container.NS != null && value.N != null)
                    return container.NS.Contains(value.N);
                if (container.BS != null && value.B != null)
                    return container.BS.Contains(value.B);
                if (container.L != null)
                    return container.L.Any(v => CompareValues(v, value) == 0);
                return false;
            }
            case "size":
            {
                // size() used in a condition context (e.g. size(path) > :val)
                // This is handled as a comparison where size returns a value
                // The function itself shouldn't be called as a boolean
                throw new ValidationException("size() cannot be used as a standalone condition");
            }
            default:
                throw new ValidationException($"Unknown function: {func.FunctionName}");
        }
    }

    private AttributeValue? EvaluateSize(FunctionNode func, Dictionary<string, AttributeValue> item)
    {
        var value = ResolveValue(func.Arguments[0], item);
        if (value == null) return null;

        int size = value.Type switch
        {
            AttributeValueType.S => value.S!.Length,
            AttributeValueType.B => value.B != null ? Convert.FromBase64String(value.B).Length : 0,
            AttributeValueType.SS => value.SS!.Count,
            AttributeValueType.NS => value.NS!.Count,
            AttributeValueType.BS => value.BS!.Count,
            AttributeValueType.L => value.L!.Count,
            AttributeValueType.M => value.M!.Count,
            _ => throw new ValidationException("Invalid operand type for size()")
        };

        return new AttributeValue { N = size.ToString() };
    }

    private DocumentPath GetPathFromArg(ExpressionNode arg)
    {
        if (arg is PathNode pathNode)
            return pathNode.Path;
        throw new ValidationException("Expected a document path argument");
    }

    private AttributeValue ResolveValuePlaceholder(string placeholder)
    {
        if (expressionAttributeValues == null || !expressionAttributeValues.TryGetValue(placeholder, out var value))
            throw new ValidationException($"Value {placeholder} not found in ExpressionAttributeValues");
        return value;
    }

    internal static int? CompareValues(AttributeValue left, AttributeValue right)
    {
        if (left.Type != right.Type) return null;

        return left.Type switch
        {
            AttributeValueType.S => string.Compare(left.S, right.S, StringComparison.Ordinal),
            AttributeValueType.N => CompareNumbers(left.N!, right.N!),
            AttributeValueType.B => string.Compare(left.B, right.B, StringComparison.Ordinal),
            AttributeValueType.BOOL => left.BOOL!.Value.CompareTo(right.BOOL!.Value),
            _ => null
        };
    }

    private static int CompareNumbers(string left, string right)
    {
        var l = decimal.Parse(left, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
        var r = decimal.Parse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
        return l.CompareTo(r);
    }
}
