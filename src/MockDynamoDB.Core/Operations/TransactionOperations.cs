using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class TransactionOperations(ITableStore tableStore, IItemStore itemStore, ReaderWriterLockSlim rwLock)
{
    public JsonDocument TransactWriteItems(JsonDocument request)
    {
        var root = request.RootElement;
        var transactItems = root.GetProperty("TransactItems");
        var items = transactItems.EnumerateArray().ToList();

        if (items.Count > 100)
            throw new ValidationException("Member must have length less than or equal to 100");

        // Validate no duplicate items
        var seenItems = new HashSet<string>();
        foreach (var item in items)
        {
            var key = GetTransactWriteItemKey(item);
            if (!seenItems.Add(key))
                throw new ValidationException("Transaction request cannot include multiple operations on one item");
        }

        rwLock.EnterWriteLock();
        try
        {
            // Phase 1: Evaluate all conditions
            var reasons = new List<CancellationReason>();
            bool anyFailed = false;

            foreach (var item in items)
            {
                var reason = EvaluateTransactWriteCondition(item);
                reasons.Add(reason);
                if (reason.Code != "None")
                    anyFailed = true;
            }

            if (anyFailed)
                throw new Models.TransactionCanceledException(reasons);

            // Phase 2: Apply all writes
            for (int i = 0; i < items.Count; i++)
            {
                ApplyTransactWrite(items[i]);
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }

        return ItemOperations.BuildEmptyResponse();
    }

    public JsonDocument TransactGetItems(JsonDocument request)
    {
        var root = request.RootElement;
        var transactItems = root.GetProperty("TransactItems");
        var items = transactItems.EnumerateArray().ToList();

        if (items.Count > 100)
            throw new ValidationException("Member must have length less than or equal to 100");

        rwLock.EnterReadLock();
        try
        {
            var responses = new List<Dictionary<string, AttributeValue>?>();

            foreach (var item in items)
            {
                var get = item.GetProperty("Get");
                var tableName = get.GetProperty("TableName").GetString()!;
                tableStore.GetTable(tableName);
                var key = ItemOperations.DeserializeItem(get.GetProperty("Key"));

                var result = itemStore.GetItem(tableName, key);

                if (result != null && get.TryGetProperty("ProjectionExpression", out var pe))
                {
                    var expressionAttributeNames = get.TryGetProperty("ExpressionAttributeNames", out var ean)
                        ? ItemOperations.DeserializeStringMap(ean) : null;
                    result = ItemOperations.ApplyProjection(result, pe.GetString()!, expressionAttributeNames);
                }

                responses.Add(result);
            }

            return BuildTransactGetResponse(responses);
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    private string GetTransactWriteItemKey(JsonElement item)
    {
        JsonElement op;
        string tableName;
        string keyJson;

        if (item.TryGetProperty("Put", out op))
        {
            tableName = op.GetProperty("TableName").GetString()!;
            var table = tableStore.GetTable(tableName);
            var putItem = ItemOperations.DeserializeItem(op.GetProperty("Item"));
            var key = ItemOperations.ExtractKey(putItem, table);
            keyJson = JsonSerializer.Serialize(key, ItemOperations.JsonOptions);
        }
        else if (item.TryGetProperty("Delete", out op))
        {
            tableName = op.GetProperty("TableName").GetString()!;
            keyJson = op.GetProperty("Key").GetRawText();
        }
        else if (item.TryGetProperty("Update", out op))
        {
            tableName = op.GetProperty("TableName").GetString()!;
            keyJson = op.GetProperty("Key").GetRawText();
        }
        else if (item.TryGetProperty("ConditionCheck", out op))
        {
            tableName = op.GetProperty("TableName").GetString()!;
            keyJson = op.GetProperty("Key").GetRawText();
        }
        else
        {
            throw new ValidationException("Invalid TransactWriteItem");
        }

        return $"{tableName}:{keyJson}";
    }

    private CancellationReason EvaluateTransactWriteCondition(JsonElement item)
    {
        JsonElement op;

        if (item.TryGetProperty("Put", out op))
            return EvaluateCondition(op, isConditionCheck: false);
        if (item.TryGetProperty("Delete", out op))
            return EvaluateCondition(op, isConditionCheck: false);
        if (item.TryGetProperty("Update", out op))
            return EvaluateCondition(op, isConditionCheck: false);
        if (item.TryGetProperty("ConditionCheck", out op))
            return EvaluateCondition(op, isConditionCheck: true);

        return new CancellationReason { Code = "None" };
    }

    private CancellationReason EvaluateCondition(JsonElement op, bool isConditionCheck)
    {
        if (!op.TryGetProperty("ConditionExpression", out var ce))
            return new CancellationReason { Code = "None" };

        var tableName = op.GetProperty("TableName").GetString()!;
        tableStore.GetTable(tableName);

        Dictionary<string, AttributeValue> key;
        if (op.TryGetProperty("Key", out var keyProp))
        {
            key = ItemOperations.DeserializeItem(keyProp);
        }
        else if (op.TryGetProperty("Item", out var itemProp))
        {
            var table = tableStore.GetTable(tableName);
            var putItem = ItemOperations.DeserializeItem(itemProp);
            key = ItemOperations.ExtractKey(putItem, table);
        }
        else
        {
            return new CancellationReason { Code = "None" };
        }

        var existingItem = itemStore.GetItem(tableName, key);

        var expressionAttributeNames = op.TryGetProperty("ExpressionAttributeNames", out var ean)
            ? ItemOperations.DeserializeStringMap(ean) : null;
        var expressionAttributeValues = op.TryGetProperty("ExpressionAttributeValues", out var eav)
            ? ItemOperations.DeserializeItem(eav) : null;

        var ast = DynamoDbExpressionParser.ParseCondition(ce.GetString()!, expressionAttributeNames);
        var evaluator = new ConditionEvaluator(expressionAttributeValues);

        var itemToCheck = existingItem ?? new Dictionary<string, AttributeValue>();
        if (!evaluator.Evaluate(ast, itemToCheck))
        {
            return new CancellationReason
            {
                Code = "ConditionalCheckFailed",
                Message = "The conditional request failed"
            };
        }

        return new CancellationReason { Code = "None" };
    }

    private void ApplyTransactWrite(JsonElement item)
    {
        if (item.TryGetProperty("Put", out var put))
        {
            var tableName = put.GetProperty("TableName").GetString()!;
            var putItem = ItemOperations.DeserializeItem(put.GetProperty("Item"));
            itemStore.PutItem(tableName, putItem);
        }
        else if (item.TryGetProperty("Delete", out var del))
        {
            var tableName = del.GetProperty("TableName").GetString()!;
            var key = ItemOperations.DeserializeItem(del.GetProperty("Key"));
            itemStore.DeleteItem(tableName, key);
        }
        else if (item.TryGetProperty("Update", out var upd))
        {
            var tableName = upd.GetProperty("TableName").GetString()!;
            var table = tableStore.GetTable(tableName);
            var key = ItemOperations.DeserializeItem(upd.GetProperty("Key"));

            var existingItem = itemStore.GetItem(tableName, key);
            var updateItem = existingItem ?? new Dictionary<string, AttributeValue>();
            if (existingItem == null)
            {
                foreach (var kv in key)
                    updateItem[kv.Key] = kv.Value.DeepClone();
            }

            if (upd.TryGetProperty("UpdateExpression", out var ue))
            {
                var expressionAttributeNames = upd.TryGetProperty("ExpressionAttributeNames", out var ean)
                    ? ItemOperations.DeserializeStringMap(ean) : null;
                var expressionAttributeValues = upd.TryGetProperty("ExpressionAttributeValues", out var eav)
                    ? ItemOperations.DeserializeItem(eav) : null;

                var actions = DynamoDbExpressionParser.ParseUpdate(ue.GetString()!, expressionAttributeNames);
                var evaluator = new UpdateEvaluator(expressionAttributeValues);
                evaluator.Apply(actions, updateItem);
            }

            itemStore.PutItem(tableName, updateItem);
        }
        // ConditionCheck has no write side-effect
    }

    private static JsonDocument BuildTransactGetResponse(List<Dictionary<string, AttributeValue>?> responses)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        writer.WritePropertyName("Responses");
        writer.WriteStartArray();
        foreach (var item in responses)
        {
            writer.WriteStartObject();
            if (item != null)
            {
                writer.WritePropertyName("Item");
                ItemOperations.WriteItem(writer, item);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }
}
