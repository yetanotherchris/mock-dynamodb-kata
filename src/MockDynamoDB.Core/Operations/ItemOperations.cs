using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public class ItemOperations
{
    private readonly ITableStore _tableStore;
    private readonly IItemStore _itemStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public ItemOperations(ITableStore tableStore, IItemStore itemStore)
    {
        _tableStore = tableStore;
        _itemStore = itemStore;
    }

    public JsonDocument PutItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var table = _tableStore.GetTable(tableName);
        var item = DeserializeItem(root.GetProperty("Item"));

        ValidateKeyAttributes(item, table);

        string? returnValues = null;
        if (root.TryGetProperty("ReturnValues", out var rv))
            returnValues = rv.GetString();

        var oldItem = _itemStore.GetItem(tableName, ExtractKey(item, table));
        _itemStore.PutItem(tableName, item);

        return BuildItemResponse(returnValues == "ALL_OLD" ? oldItem : null);
    }

    public JsonDocument GetItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        _tableStore.GetTable(tableName);
        var key = DeserializeItem(root.GetProperty("Key"));

        var item = _itemStore.GetItem(tableName, key);

        if (item == null)
            return BuildEmptyResponse();

        string? projectionExpression = null;
        Dictionary<string, string>? expressionAttributeNames = null;

        if (root.TryGetProperty("ProjectionExpression", out var pe))
            projectionExpression = pe.GetString();
        if (root.TryGetProperty("ExpressionAttributeNames", out var ean))
            expressionAttributeNames = DeserializeStringMap(ean);

        if (projectionExpression != null)
            item = ApplySimpleProjection(item, projectionExpression, expressionAttributeNames);

        return BuildGetItemResponse(item);
    }

    public JsonDocument DeleteItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        _tableStore.GetTable(tableName);
        var key = DeserializeItem(root.GetProperty("Key"));

        string? returnValues = null;
        if (root.TryGetProperty("ReturnValues", out var rv))
            returnValues = rv.GetString();

        var oldItem = _itemStore.DeleteItem(tableName, key);

        return BuildItemResponse(returnValues == "ALL_OLD" ? oldItem : null);
    }

    internal static Dictionary<string, AttributeValue> DeserializeItem(JsonElement element)
    {
        var item = new Dictionary<string, AttributeValue>();
        foreach (var prop in element.EnumerateObject())
        {
            item[prop.Name] = JsonSerializer.Deserialize<AttributeValue>(prop.Value.GetRawText(), JsonOptions)!;
        }
        return item;
    }

    internal static Dictionary<string, string> DeserializeStringMap(JsonElement element)
    {
        var map = new Dictionary<string, string>();
        foreach (var prop in element.EnumerateObject())
        {
            map[prop.Name] = prop.Value.GetString()!;
        }
        return map;
    }

    internal static void ValidateKeyAttributes(Dictionary<string, AttributeValue> item, TableDefinition table)
    {
        if (!item.ContainsKey(table.HashKeyName))
            throw new ValidationException(
                $"One or more parameter values were invalid: Missing the key {table.HashKeyName} in the item");

        if (table.HasRangeKey && !item.ContainsKey(table.RangeKeyName!))
            throw new ValidationException(
                $"One or more parameter values were invalid: Missing the key {table.RangeKeyName} in the item");
    }

    internal static Dictionary<string, AttributeValue> ExtractKey(
        Dictionary<string, AttributeValue> item, TableDefinition table)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            [table.HashKeyName] = item[table.HashKeyName]
        };
        if (table.HasRangeKey)
            key[table.RangeKeyName!] = item[table.RangeKeyName!];
        return key;
    }

    internal static Dictionary<string, AttributeValue> ApplySimpleProjection(
        Dictionary<string, AttributeValue> item,
        string projectionExpression,
        Dictionary<string, string>? expressionAttributeNames)
    {
        var attrs = projectionExpression.Split(',').Select(s => s.Trim()).ToList();
        var result = new Dictionary<string, AttributeValue>();

        foreach (var attr in attrs)
        {
            var resolvedName = attr;
            if (expressionAttributeNames != null && expressionAttributeNames.TryGetValue(attr, out var mapped))
                resolvedName = mapped;

            if (resolvedName.Contains('.'))
            {
                ApplyNestedProjection(item, resolvedName, result);
            }
            else if (item.TryGetValue(resolvedName, out var value))
            {
                result[resolvedName] = value.DeepClone();
            }
        }

        return result;
    }

    private static void ApplyNestedProjection(
        Dictionary<string, AttributeValue> item,
        string path,
        Dictionary<string, AttributeValue> result)
    {
        var parts = path.Split('.');
        if (!item.TryGetValue(parts[0], out var current))
            return;

        var nav = current;
        for (int i = 1; i < parts.Length; i++)
        {
            if (nav.M == null || !nav.M.TryGetValue(parts[i], out var next))
                return;
            nav = next;
        }

        // Build the nested structure in result
        if (!result.ContainsKey(parts[0]))
            result[parts[0]] = new AttributeValue { M = new Dictionary<string, AttributeValue>() };

        var target = result[parts[0]];
        for (int i = 1; i < parts.Length - 1; i++)
        {
            if (target.M == null)
                target.M = new Dictionary<string, AttributeValue>();
            if (!target.M.ContainsKey(parts[i]))
                target.M[parts[i]] = new AttributeValue { M = new Dictionary<string, AttributeValue>() };
            target = target.M[parts[i]];
        }

        if (target.M == null)
            target.M = new Dictionary<string, AttributeValue>();
        target.M[parts[^1]] = nav.DeepClone();
    }

    internal static JsonDocument BuildItemResponse(Dictionary<string, AttributeValue>? attributes)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        if (attributes != null)
        {
            writer.WritePropertyName("Attributes");
            WriteItem(writer, attributes);
        }
        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    internal static JsonDocument BuildGetItemResponse(Dictionary<string, AttributeValue> item)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WritePropertyName("Item");
        WriteItem(writer, item);
        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    internal static JsonDocument BuildEmptyResponse()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    internal static void WriteItem(Utf8JsonWriter writer, Dictionary<string, AttributeValue> item)
    {
        var json = JsonSerializer.Serialize(item, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.WriteTo(writer);
    }

    internal static void WriteItemsList(Utf8JsonWriter writer, List<Dictionary<string, AttributeValue>> items)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            WriteItem(writer, item);
        }
        writer.WriteEndArray();
    }
}
