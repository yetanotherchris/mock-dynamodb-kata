using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class ItemOperations(ITableStore tableStore, IItemStore itemStore)
{
    internal static readonly JsonSerializerOptions JsonOptions = DynamoDbJsonOptions.Options;

    public PutItemResponse PutItem(PutItemRequest request)
    {
        var table = tableStore.GetTable(request.TableName);
        var item = request.Item;

        ValidateKeyAttributes(item, table);

        var key = ExtractKey(item, table);
        var oldItem = itemStore.GetItem(request.TableName, key);

        EvaluateConditionExpression(request.ConditionExpression, request.ExpressionAttributeNames,
            request.ExpressionAttributeValues, oldItem);
        if (request.Expected is JsonElement exp)
            PreExpressionRequestParser.EvaluateExpected(exp, request.ConditionalOperator, oldItem);

        itemStore.PutItem(request.TableName, item);

        return new PutItemResponse
        {
            Attributes = request.ReturnValues == "ALL_OLD" ? oldItem : null
        };
    }

    public GetItemResponse GetItem(GetItemRequest request)
    {
        tableStore.GetTable(request.TableName);
        var item = itemStore.GetItem(request.TableName, request.Key);

        if (item == null)
            return new GetItemResponse();

        if (request.ProjectionExpression != null)
            item = ApplyProjection(item, request.ProjectionExpression, request.ExpressionAttributeNames);

        return new GetItemResponse { Item = item };
    }

    public DeleteItemResponse DeleteItem(DeleteItemRequest request)
    {
        tableStore.GetTable(request.TableName);

        var oldItem = itemStore.GetItem(request.TableName, request.Key);
        EvaluateConditionExpression(request.ConditionExpression, request.ExpressionAttributeNames,
            request.ExpressionAttributeValues, oldItem);
        if (request.Expected is JsonElement exp)
            PreExpressionRequestParser.EvaluateExpected(exp, request.ConditionalOperator, oldItem);

        var deleted = itemStore.DeleteItem(request.TableName, request.Key);

        return new DeleteItemResponse
        {
            Attributes = request.ReturnValues == "ALL_OLD" ? deleted : null
        };
    }

    public UpdateItemResponse UpdateItem(UpdateItemRequest request)
    {
        var table = tableStore.GetTable(request.TableName);

        var existingItem = itemStore.GetItem(request.TableName, request.Key);
        EvaluateConditionExpression(request.ConditionExpression, request.ExpressionAttributeNames,
            request.ExpressionAttributeValues, existingItem);
        if (request.Expected is JsonElement exp)
            PreExpressionRequestParser.EvaluateExpected(exp, request.ConditionalOperator, existingItem);

        // Build item (upsert: create with key if doesn't exist)
        var item = existingItem ?? new Dictionary<string, AttributeValue>();
        if (existingItem == null)
        {
            foreach (var kv in request.Key)
                item[kv.Key] = kv.Value.DeepClone();
        }

        // Capture old values for UPDATED_OLD
        var oldItem = existingItem?.CloneItem();

        // UpdateExpression (expression format) or AttributeUpdates (pre-expression format)
        if (request.UpdateExpression != null)
        {
            var actions = DynamoDbExpressionParser.ParseUpdate(request.UpdateExpression, request.ExpressionAttributeNames);
            var evaluator = new UpdateEvaluator(request.ExpressionAttributeValues);
            evaluator.Apply(actions, item);
        }
        else if (request.AttributeUpdates is JsonElement au)
        {
            PreExpressionRequestParser.ApplyAttributeUpdates(au, item);
        }

        itemStore.PutItem(request.TableName, item);

        // Determine return value
        Dictionary<string, AttributeValue>? returnItem = request.ReturnValues switch
        {
            "ALL_OLD" => oldItem,
            "ALL_NEW" => item.CloneItem(),
            "UPDATED_OLD" => GetUpdatedAttributes(oldItem, item, returnOld: true),
            "UPDATED_NEW" => GetUpdatedAttributes(oldItem, item, returnOld: false),
            _ => null
        };

        return new UpdateItemResponse { Attributes = returnItem };
    }

    private static void EvaluateConditionExpression(
        string? conditionExpression,
        Dictionary<string, string>? expressionAttributeNames,
        Dictionary<string, AttributeValue>? expressionAttributeValues,
        Dictionary<string, AttributeValue>? existingItem)
    {
        if (conditionExpression == null)
            return;

        var ast = DynamoDbExpressionParser.ParseCondition(conditionExpression, expressionAttributeNames);
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
}
