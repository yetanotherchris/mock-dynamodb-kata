using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class DocumentPath
{
    public List<PathElement> Elements { get; } = [];

    public DocumentPath() { }

    public DocumentPath(List<PathElement> elements)
    {
        Elements = elements;
    }

    public static List<DocumentPath> ParseProjection(string expression,
        Dictionary<string, string>? expressionAttributeNames = null)
    {
        var paths = new List<DocumentPath>();
        var parts = expression.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            paths.Add(ParseSinglePath(trimmed, expressionAttributeNames));
        }

        return paths;
    }

    private static DocumentPath ParseSinglePath(string expression,
        Dictionary<string, string>? expressionAttributeNames)
    {
        var input = new Antlr4.Runtime.AntlrInputStream(expression);
        var lexer = new Grammar.DynamoDbConditionLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(DynamoDbExpressionParser.ThrowingErrorListener.Instance);

        var tokens = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new Grammar.DynamoDbConditionParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(DynamoDbExpressionParser.ThrowingErrorListener.Instance);

        var tree = parser.documentPath();
        return ConditionExpressionVisitor.BuildDocumentPath(tree, expressionAttributeNames);
    }

    internal static string ResolveName(string placeholder, Dictionary<string, string>? names)
    {
        if (names == null || !names.TryGetValue(placeholder, out var resolved))
            throw new ValidationException(
                $"Value provided in ExpressionAttributeNames unused in expressions: keys: {{{placeholder}}}");
        return resolved;
    }

    public AttributeValue? Resolve(Dictionary<string, AttributeValue> item)
    {
        if (Elements.Count == 0) return null;

        var firstElement = (AttributeElement)Elements[0];
        if (!item.TryGetValue(firstElement.Name, out var current))
            return null;

        for (int i = 1; i < Elements.Count; i++)
        {
            if (current == null) return null;

            switch (Elements[i])
            {
                case AttributeElement attr:
                    if (current.M == null) return null;
                    if (!current.M.TryGetValue(attr.Name, out current))
                        return null;
                    break;
                case IndexElement idx:
                    if (current.L == null || idx.Index < 0 || idx.Index >= current.L.Count)
                        return null;
                    current = current.L[idx.Index];
                    break;
            }
        }

        return current;
    }

    public void SetValue(Dictionary<string, AttributeValue> item, AttributeValue value)
    {
        if (Elements.Count == 0) return;

        if (Elements.Count == 1)
        {
            var name = ((AttributeElement)Elements[0]).Name;
            item[name] = value;
            return;
        }

        var firstElement = (AttributeElement)Elements[0];
        if (!item.TryGetValue(firstElement.Name, out var current))
        {
            current = new AttributeValue { M = new Dictionary<string, AttributeValue>() };
            item[firstElement.Name] = current;
        }

        for (int i = 1; i < Elements.Count - 1; i++)
        {
            switch (Elements[i])
            {
                case AttributeElement attr:
                    if (current.M == null)
                    {
                        current.M = new Dictionary<string, AttributeValue>();
                    }
                    if (!current.M.TryGetValue(attr.Name, out var next))
                    {
                        next = new AttributeValue { M = new Dictionary<string, AttributeValue>() };
                        current.M[attr.Name] = next;
                    }
                    current = next;
                    break;
                case IndexElement idx:
                    if (current.L == null || idx.Index < 0 || idx.Index >= current.L.Count)
                        throw new ValidationException($"List index out of bounds: {idx.Index}");
                    current = current.L[idx.Index];
                    break;
            }
        }

        var lastElement = Elements[^1];
        switch (lastElement)
        {
            case AttributeElement attr:
                if (current.M == null)
                    current.M = new Dictionary<string, AttributeValue>();
                current.M[attr.Name] = value;
                break;
            case IndexElement idx:
                if (current.L == null || idx.Index < 0 || idx.Index >= current.L.Count)
                    throw new ValidationException($"List index out of bounds: {idx.Index}");
                current.L[idx.Index] = value;
                break;
        }
    }

    public void Remove(Dictionary<string, AttributeValue> item)
    {
        if (Elements.Count == 0) return;

        if (Elements.Count == 1)
        {
            var name = ((AttributeElement)Elements[0]).Name;
            item.Remove(name);
            return;
        }

        var firstElement = (AttributeElement)Elements[0];
        if (!item.TryGetValue(firstElement.Name, out var current))
            return;

        for (int i = 1; i < Elements.Count - 1; i++)
        {
            switch (Elements[i])
            {
                case AttributeElement attr:
                    if (current.M == null || !current.M.TryGetValue(attr.Name, out current))
                        return;
                    break;
                case IndexElement idx:
                    if (current.L == null || idx.Index < 0 || idx.Index >= current.L.Count)
                        return;
                    current = current.L[idx.Index];
                    break;
            }
        }

        var lastElement = Elements[^1];
        switch (lastElement)
        {
            case AttributeElement attr:
                current.M?.Remove(attr.Name);
                break;
            case IndexElement idx:
                if (current.L != null && idx.Index >= 0 && idx.Index < current.L.Count)
                    current.L.RemoveAt(idx.Index);
                break;
        }
    }
}

public abstract record PathElement;

public sealed record AttributeElement(string Name) : PathElement;

public sealed record IndexElement(int Index) : PathElement;
