using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class DocumentPath
{
    public List<PathElement> Elements { get; } = [];

    public static DocumentPath Parse(List<Token> tokens, ref int pos,
        Dictionary<string, string>? expressionAttributeNames = null)
    {
        var path = new DocumentPath();

        // First element must be an identifier or name placeholder
        var token = tokens[pos];
        if (token.Type == TokenType.NamePlaceholder)
        {
            var name = ResolveName(token.Value, expressionAttributeNames);
            path.Elements.Add(new AttributeElement(name));
            pos++;
        }
        else if (token.Type == TokenType.Identifier)
        {
            path.Elements.Add(new AttributeElement(token.Value));
            pos++;
        }
        else
        {
            throw new ValidationException($"Expected attribute name at position {token.Position}");
        }

        // Continue with dots and brackets
        while (pos < tokens.Count)
        {
            token = tokens[pos];
            if (token.Type == TokenType.Dot)
            {
                pos++;
                token = tokens[pos];
                if (token.Type == TokenType.NamePlaceholder)
                {
                    var name = ResolveName(token.Value, expressionAttributeNames);
                    path.Elements.Add(new AttributeElement(name));
                    pos++;
                }
                else if (token.Type == TokenType.Identifier)
                {
                    path.Elements.Add(new AttributeElement(token.Value));
                    pos++;
                }
                else
                {
                    throw new ValidationException($"Expected attribute name after '.' at position {token.Position}");
                }
            }
            else if (token.Type == TokenType.LeftBracket)
            {
                pos++;
                token = tokens[pos];
                if (token.Type != TokenType.Number)
                    throw new ValidationException($"Expected numeric index at position {token.Position}");
                path.Elements.Add(new IndexElement(int.Parse(token.Value)));
                pos++;
                token = tokens[pos];
                if (token.Type != TokenType.RightBracket)
                    throw new ValidationException($"Expected ']' at position {token.Position}");
                pos++;
            }
            else
            {
                break;
            }
        }

        return path;
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

            var tokenizer = new Tokenizer(trimmed);
            var tokens = tokenizer.Tokenize();
            var pos = 0;
            paths.Add(Parse(tokens, ref pos, expressionAttributeNames));
        }

        return paths;
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

    private static string ResolveName(string placeholder, Dictionary<string, string>? names)
    {
        if (names == null || !names.TryGetValue(placeholder, out var resolved))
            throw new ValidationException(
                $"Value provided in ExpressionAttributeNames unused in expressions: keys: {{{placeholder}}}");
        return resolved;
    }
}

public abstract class PathElement { }

public class AttributeElement : PathElement
{
    public string Name { get; }
    public AttributeElement(string name) { Name = name; }
}

public class IndexElement : PathElement
{
    public int Index { get; }
    public IndexElement(int index) { Index = index; }
}
