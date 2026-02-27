using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public sealed class UpdateEvaluator(Dictionary<string, AttributeValue>? expressionAttributeValues)
{
    public void Apply(List<UpdateAction> actions, Dictionary<string, AttributeValue> item)
    {
        foreach (var action in actions)
        {
            switch (action.Type)
            {
                case "SET":
                    ApplySet(action, item);
                    break;
                case "REMOVE":
                    action.Path.Remove(item);
                    break;
                case "ADD":
                    ApplyAdd(action, item);
                    break;
                case "DELETE":
                    ApplyDelete(action, item);
                    break;
            }
        }
    }

    private void ApplySet(UpdateAction action, Dictionary<string, AttributeValue> item)
    {
        var value = ResolveSetValue(action.Value!, item);
        action.Path.SetValue(item, value);
    }

    private AttributeValue ResolveSetValue(ExpressionNode node, Dictionary<string, AttributeValue> item)
    {
        switch (node)
        {
            case ValuePlaceholderNode placeholder:
                return ResolveValuePlaceholder(placeholder.Placeholder);

            case PathNode pathNode:
                var resolved = pathNode.Path.Resolve(item);
                if (resolved == null)
                    throw new ValidationException("The provided expression refers to an attribute that does not exist in the item");
                return resolved.DeepClone();

            case ArithmeticNode arithmetic:
                return EvaluateArithmetic(arithmetic, item);

            case FunctionNode func:
                return EvaluateSetFunction(func, item);

            default:
                throw new ValidationException($"Unsupported SET value node: {node.GetType().Name}");
        }
    }

    private AttributeValue EvaluateArithmetic(ArithmeticNode node, Dictionary<string, AttributeValue> item)
    {
        var left = ResolveSetValue(node.Left, item);
        var right = ResolveSetValue(node.Right, item);

        if (left.N == null || right.N == null)
            throw new ValidationException("An operand in the update expression has an incorrect data type");

        var l = decimal.Parse(left.N, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
        var r = decimal.Parse(right.N, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);

        var result = node.Operator switch
        {
            "+" => l + r,
            "-" => l - r,
            _ => throw new ValidationException($"Unknown arithmetic operator: {node.Operator}")
        };

        return new AttributeValue { N = result.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }

    private AttributeValue EvaluateSetFunction(FunctionNode func, Dictionary<string, AttributeValue> item)
    {
        switch (func.FunctionName)
        {
            case "if_not_exists":
            {
                var path = GetPathFromArg(func.Arguments[0]);
                var existing = path.Resolve(item);
                if (existing != null)
                    return existing.DeepClone();
                return ResolveSetValue(func.Arguments[1], item);
            }
            case "list_append":
            {
                var left = ResolveSetValue(func.Arguments[0], item);
                var right = ResolveSetValue(func.Arguments[1], item);

                if (left.L == null || right.L == null)
                    throw new ValidationException("An operand in the update expression has an incorrect data type");

                var combined = new List<AttributeValue>(left.L);
                combined.AddRange(right.L);
                return new AttributeValue { L = combined };
            }
            default:
                throw new ValidationException($"Unsupported function in SET: {func.FunctionName}");
        }
    }

    private void ApplyAdd(UpdateAction action, Dictionary<string, AttributeValue> item)
    {
        var addValue = ResolveSetValue(action.Value!, item);
        var existing = action.Path.Resolve(item);

        if (existing == null)
        {
            // If attribute doesn't exist, SET it
            action.Path.SetValue(item, addValue.DeepClone());
            return;
        }

        if (existing.N != null && addValue.N != null)
        {
            var current = decimal.Parse(existing.N, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
            var add = decimal.Parse(addValue.N, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
            action.Path.SetValue(item, new AttributeValue
            {
                N = (current + add).ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        else if (existing.SS != null && addValue.SS != null)
        {
            var set = new HashSet<string>(existing.SS);
            foreach (var s in addValue.SS) set.Add(s);
            action.Path.SetValue(item, new AttributeValue { SS = set.ToList() });
        }
        else if (existing.NS != null && addValue.NS != null)
        {
            var set = new HashSet<string>(existing.NS);
            foreach (var s in addValue.NS) set.Add(s);
            action.Path.SetValue(item, new AttributeValue { NS = set.ToList() });
        }
        else if (existing.BS != null && addValue.BS != null)
        {
            var set = new HashSet<string>(existing.BS);
            foreach (var s in addValue.BS) set.Add(s);
            action.Path.SetValue(item, new AttributeValue { BS = set.ToList() });
        }
        else
        {
            throw new ValidationException("An operand in the update expression has an incorrect data type");
        }
    }

    private void ApplyDelete(UpdateAction action, Dictionary<string, AttributeValue> item)
    {
        var deleteValue = ResolveSetValue(action.Value!, item);
        var existing = action.Path.Resolve(item);

        if (existing == null) return;

        if (existing.SS != null && deleteValue.SS != null)
        {
            var set = new HashSet<string>(existing.SS);
            foreach (var s in deleteValue.SS) set.Remove(s);
            if (set.Count == 0)
                action.Path.Remove(item);
            else
                action.Path.SetValue(item, new AttributeValue { SS = set.ToList() });
        }
        else if (existing.NS != null && deleteValue.NS != null)
        {
            var set = new HashSet<string>(existing.NS);
            foreach (var s in deleteValue.NS) set.Remove(s);
            if (set.Count == 0)
                action.Path.Remove(item);
            else
                action.Path.SetValue(item, new AttributeValue { NS = set.ToList() });
        }
        else if (existing.BS != null && deleteValue.BS != null)
        {
            var set = new HashSet<string>(existing.BS);
            foreach (var s in deleteValue.BS) set.Remove(s);
            if (set.Count == 0)
                action.Path.Remove(item);
            else
                action.Path.SetValue(item, new AttributeValue { BS = set.ToList() });
        }
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
        return value.DeepClone();
    }
}
