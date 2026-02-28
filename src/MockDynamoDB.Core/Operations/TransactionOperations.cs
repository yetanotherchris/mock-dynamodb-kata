using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class TransactionOperations(ITableStore tableStore, IItemStore itemStore, ReaderWriterLockSlim rwLock)
{
    public TransactWriteItemsResponse TransactWriteItems(TransactWriteItemsRequest request)
    {
        var items = request.TransactItems;

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
            foreach (var item in items)
            {
                ApplyTransactWrite(item);
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }

        return new TransactWriteItemsResponse();
    }

    public TransactGetItemsResponse TransactGetItems(TransactGetItemsRequest request)
    {
        var items = request.TransactItems;

        if (items.Count > 100)
            throw new ValidationException("Member must have length less than or equal to 100");

        rwLock.EnterReadLock();
        try
        {
            var responses = new List<ItemResponse>();

            foreach (var item in items)
            {
                var get = item.Get;
                tableStore.GetTable(get.TableName);

                var result = itemStore.GetItem(get.TableName, get.Key);

                if (result != null && get.ProjectionExpression != null)
                {
                    result = ItemOperations.ApplyProjection(result, get.ProjectionExpression, get.ExpressionAttributeNames);
                }

                responses.Add(new ItemResponse { Item = result });
            }

            return new TransactGetItemsResponse { Responses = responses };
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    private string GetTransactWriteItemKey(TransactWriteItem item)
    {
        if (item.Put is { } put)
        {
            var table = tableStore.GetTable(put.TableName);
            var key = ItemOperations.ExtractKey(put.Item, table);
            var keyJson = JsonSerializer.Serialize(key, ItemOperations.JsonOptions);
            return $"{put.TableName}:{keyJson}";
        }
        if (item.Delete is { } del)
        {
            var keyJson = JsonSerializer.Serialize(del.Key, ItemOperations.JsonOptions);
            return $"{del.TableName}:{keyJson}";
        }
        if (item.Update is { } upd)
        {
            var keyJson = JsonSerializer.Serialize(upd.Key, ItemOperations.JsonOptions);
            return $"{upd.TableName}:{keyJson}";
        }
        if (item.ConditionCheck is { } cc)
        {
            var keyJson = JsonSerializer.Serialize(cc.Key, ItemOperations.JsonOptions);
            return $"{cc.TableName}:{keyJson}";
        }

        throw new ValidationException("Invalid TransactWriteItem");
    }

    private CancellationReason EvaluateTransactWriteCondition(TransactWriteItem item)
    {
        if (item.Put is { } put)
            return EvaluateCondition(put.TableName, put.ConditionExpression, null, put.Item,
                put.ExpressionAttributeNames, put.ExpressionAttributeValues);
        if (item.Delete is { } del)
            return EvaluateCondition(del.TableName, del.ConditionExpression, del.Key, null,
                del.ExpressionAttributeNames, del.ExpressionAttributeValues);
        if (item.Update is { } upd)
            return EvaluateCondition(upd.TableName, upd.ConditionExpression, upd.Key, null,
                upd.ExpressionAttributeNames, upd.ExpressionAttributeValues);
        if (item.ConditionCheck is { } cc)
            return EvaluateCondition(cc.TableName, cc.ConditionExpression, cc.Key, null,
                cc.ExpressionAttributeNames, cc.ExpressionAttributeValues);

        return new CancellationReason { Code = "None" };
    }

    private CancellationReason EvaluateCondition(
        string tableName,
        string? conditionExpression,
        Dictionary<string, AttributeValue>? key,
        Dictionary<string, AttributeValue>? putItem,
        Dictionary<string, string>? expressionAttributeNames,
        Dictionary<string, AttributeValue>? expressionAttributeValues)
    {
        if (conditionExpression == null)
            return new CancellationReason { Code = "None" };

        tableStore.GetTable(tableName);

        Dictionary<string, AttributeValue> resolvedKey;
        if (key != null)
        {
            resolvedKey = key;
        }
        else if (putItem != null)
        {
            var table = tableStore.GetTable(tableName);
            resolvedKey = ItemOperations.ExtractKey(putItem, table);
        }
        else
        {
            return new CancellationReason { Code = "None" };
        }

        var existingItem = itemStore.GetItem(tableName, resolvedKey);

        var ast = DynamoDbExpressionParser.ParseCondition(conditionExpression, expressionAttributeNames);
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

    private void ApplyTransactWrite(TransactWriteItem item)
    {
        if (item.Put is { } put)
        {
            itemStore.PutItem(put.TableName, put.Item);
        }
        else if (item.Delete is { } del)
        {
            itemStore.DeleteItem(del.TableName, del.Key);
        }
        else if (item.Update is { } upd)
        {
            var table = tableStore.GetTable(upd.TableName);

            var existingItem = itemStore.GetItem(upd.TableName, upd.Key);
            var updateItem = existingItem ?? new Dictionary<string, AttributeValue>();
            if (existingItem == null)
            {
                foreach (var kv in upd.Key)
                    updateItem[kv.Key] = kv.Value.DeepClone();
            }

            if (upd.UpdateExpression != null)
            {
                var actions = DynamoDbExpressionParser.ParseUpdate(upd.UpdateExpression, upd.ExpressionAttributeNames);
                var evaluator = new UpdateEvaluator(upd.ExpressionAttributeValues);
                evaluator.Apply(actions, updateItem);
            }

            itemStore.PutItem(upd.TableName, updateItem);
        }
        // ConditionCheck has no write side-effect
    }
}
