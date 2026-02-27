using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class ItemOperations(ITableStore tableStore, IItemStore itemStore)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public JsonDocument PutItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var table = tableStore.GetTable(tableName);
        var item = DeserializeItem(root.GetProperty("Item"));

        ValidateKeyAttributes(item, table);

        string? returnValues = null;
        if (root.TryGetProperty("ReturnValues", out var rv))
            returnValues = rv.GetString();

        var key = ExtractKey(item, table);
        var oldItem = itemStore.GetItem(tableName, key);

        EvaluateConditionExpression(root, oldItem);
        if (root.TryGetProperty("Expected", out var exp))
            PreExpressionRequestParser.EvaluateExpected(exp, root, oldItem);

        itemStore.PutItem(tableName, item);

        return BuildItemResponse(returnValues == "ALL_OLD" ? oldItem : null);
    }

    public JsonDocument GetItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        tableStore.GetTable(tableName);
        var key = DeserializeItem(root.GetProperty("Key"));

        var item = itemStore.GetItem(tableName, key);

        if (item == null)
            return BuildEmptyResponse();

        string? projectionExpression = null;
        Dictionary<string, string>? expressionAttributeNames = null;

        if (root.TryGetProperty("ProjectionExpression", out var pe))
            projectionExpression = pe.GetString();
        if (root.TryGetProperty("ExpressionAttributeNames", out var ean))
            expressionAttributeNames = DeserializeStringMap(ean);

        if (projectionExpression != null)
            item = ApplyProjection(item, projectionExpression, expressionAttributeNames);

        return BuildGetItemResponse(item);
    }

    public JsonDocument DeleteItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        tableStore.GetTable(tableName);
        var key = DeserializeItem(root.GetProperty("Key"));

        string? returnValues = null;
        if (root.TryGetProperty("ReturnValues", out var rv))
            returnValues = rv.GetString();

        var oldItem = itemStore.GetItem(tableName, key);
        EvaluateConditionExpression(root, oldItem);
        if (root.TryGetProperty("Expected", out var exp))
            PreExpressionRequestParser.EvaluateExpected(exp, root, oldItem);

        var deleted = itemStore.DeleteItem(tableName, key);

        return BuildItemResponse(returnValues == "ALL_OLD" ? deleted : null);
    }

    public JsonDocument UpdateItem(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var table = tableStore.GetTable(tableName);
        var key = DeserializeItem(root.GetProperty("Key"));

        string? returnValues = null;
        if (root.TryGetProperty("ReturnValues", out var rv))
            returnValues = rv.GetString();

        var existingItem = itemStore.GetItem(tableName, key);
        EvaluateConditionExpression(root, existingItem);
        if (root.TryGetProperty("Expected", out var exp))
            PreExpressionRequestParser.EvaluateExpected(exp, root, existingItem);

        // Build item (upsert: create with key if doesn't exist)
        var item = existingItem ?? new Dictionary<string, AttributeValue>();
        if (existingItem == null)
        {
            // Add key attributes for new item
            foreach (var kv in key)
                item[kv.Key] = kv.Value.DeepClone();
        }

        // Capture old values for UPDATED_OLD
        var oldItem = existingItem != null ? existingItem.CloneItem() : null;

        // UpdateExpression (expression format) or AttributeUpdates (pre-expression format)
        if (root.TryGetProperty("UpdateExpression", out var ue))
        {
            var expressionAttributeNames = root.TryGetProperty("ExpressionAttributeNames", out var ean)
                ? DeserializeStringMap(ean) : null;
            var expressionAttributeValues = root.TryGetProperty("ExpressionAttributeValues", out var eav)
                ? DeserializeItem(eav) : null;

            var actions = DynamoDbExpressionParser.ParseUpdate(ue.GetString()!, expressionAttributeNames);
            var evaluator = new UpdateEvaluator(expressionAttributeValues);
            evaluator.Apply(actions, item);
        }
        else if (root.TryGetProperty("AttributeUpdates", out var au))
        {
            PreExpressionRequestParser.ApplyAttributeUpdates(au, item);
        }

        itemStore.PutItem(tableName, item);

        // Determine return value
        Dictionary<string, AttributeValue>? returnItem = returnValues switch
        {
            "ALL_OLD" => oldItem,
            "ALL_NEW" => item.CloneItem(),
            "UPDATED_OLD" => GetUpdatedAttributes(oldItem, item, returnOld: true),
            "UPDATED_NEW" => GetUpdatedAttributes(oldItem, item, returnOld: false),
            _ => null
        };

        return BuildItemResponse(returnItem);
    }

    private static void EvaluateConditionExpression(JsonElement root, Dictionary<string, AttributeValue>? existingItem)
    {
        if (!root.TryGetProperty("ConditionExpression", out var ce))
            return;

        var expressionAttributeNames = root.TryGetProperty("ExpressionAttributeNames", out var ean)
            ? DeserializeStringMap(ean) : null;
        var expressionAttributeValues = root.TryGetProperty("ExpressionAttributeValues", out var eav)
            ? DeserializeItem(eav) : null;

        var ast = DynamoDbExpressionParser.ParseCondition(ce.GetString()!, expressionAttributeNames);
        var evaluator = new ConditionEvaluator(expressionAttributeValues);

        var itemToCheck = existingItem ?? new Dictionary<string, AttributeValue>();
        if (!evaluator.Evaluate(ast, itemToCheck))
            throw new ConditionalCheckFailedException();
    }

    private static Dictionary<string, AttributeValue>? GetUpdatedAttributes(
        Dictionary<string, AttributeValue>? oldItem,
        Dictionary<string, AttributeValue> newItem,
        bool returnOld)
    {
        if (oldItem == null)
            return returnOld ? null : newItem.CloneItem();

        var result = new Dictionary<string, AttributeValue>();
        var allKeys = new HashSet<string>(oldItem.Keys);
        allKeys.UnionWith(newItem.Keys);

        foreach (var attrKey in allKeys)
        {
            var hadOld = oldItem.TryGetValue(attrKey, out var oldVal);
            var hasNew = newItem.TryGetValue(attrKey, out var newVal);

            bool changed;
            if (hadOld && hasNew)
                changed = !AttributeValuesEqual(oldVal!, newVal!);
            else
                changed = true;

            if (changed)
            {
                if (returnOld && hadOld)
                    result[attrKey] = oldVal!.DeepClone();
                else if (!returnOld && hasNew)
                    result[attrKey] = newVal!.DeepClone();
            }
        }

        return result;
    }

    private static bool AttributeValuesEqual(AttributeValue a, AttributeValue b)
    {
        if (a.Type != b.Type) return false;
        var jsonA = JsonSerializer.Serialize(a, JsonOptions);
        var jsonB = JsonSerializer.Serialize(b, JsonOptions);
        return jsonA == jsonB;
    }

    internal static Dictionary<string, AttributeValue> DeserializeItem(JsonElement element)
    {
        var item = new Dictionary<string, AttributeValue>();
        foreach (var prop in element.EnumerateObject())
        {
            item[prop.Name] = DeserializeAttributeValue(prop.Value);
        }
        return item;
    }

    internal static AttributeValue DeserializeAttributeValue(JsonElement element) =>
        JsonSerializer.Deserialize<AttributeValue>(element.GetRawText(), JsonOptions)!;

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

    internal static Dictionary<string, AttributeValue> ApplyProjection(
        Dictionary<string, AttributeValue> item,
        string projectionExpression,
        Dictionary<string, string>? expressionAttributeNames)
    {
        var paths = DocumentPath.ParseProjection(projectionExpression, expressionAttributeNames);
        var result = new Dictionary<string, AttributeValue>();

        foreach (var path in paths)
        {
            var value = path.Resolve(item);
            if (value == null) continue;

            if (path.Elements.Count == 1 && path.Elements[0] is AttributeElement attr)
            {
                result[attr.Name] = value.DeepClone();
            }
            else
            {
                // Nested projection - rebuild structure
                var rootAttr = ((AttributeElement)path.Elements[0]).Name;
                BuildNestedResult(result, path, value, rootAttr);
            }
        }

        return result;
    }

    private static void BuildNestedResult(
        Dictionary<string, AttributeValue> result,
        DocumentPath path,
        AttributeValue value,
        string rootAttr)
    {
        if (!result.ContainsKey(rootAttr))
            result[rootAttr] = new AttributeValue { M = new Dictionary<string, AttributeValue>() };

        var current = result[rootAttr];
        for (int i = 1; i < path.Elements.Count - 1; i++)
        {
            if (path.Elements[i] is AttributeElement attr)
            {
                if (current.M == null)
                    current.M = new Dictionary<string, AttributeValue>();
                if (!current.M.ContainsKey(attr.Name))
                    current.M[attr.Name] = new AttributeValue { M = new Dictionary<string, AttributeValue>() };
                current = current.M[attr.Name];
            }
        }

        var lastElement = path.Elements[^1];
        if (lastElement is AttributeElement lastAttr)
        {
            if (current.M == null)
                current.M = new Dictionary<string, AttributeValue>();
            current.M[lastAttr.Name] = value.DeepClone();
        }
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
