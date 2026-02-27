using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class BatchOperations(ITableStore tableStore, IItemStore itemStore)
{
    public BatchGetItemResponse BatchGetItem(BatchGetItemRequest request)
    {
        int totalKeys = 0;
        var responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>();

        foreach (var (tableName, keysAndAttributes) in request.RequestItems)
        {
            tableStore.GetTable(tableName); // validate table exists

            var tableItems = new List<Dictionary<string, AttributeValue>>();

            foreach (var key in keysAndAttributes.Keys)
            {
                totalKeys++;
                if (totalKeys > 100)
                    throw new ValidationException("Too many items requested for the BatchGetItem call");

                var item = itemStore.GetItem(tableName, key);
                if (item != null)
                {
                    if (keysAndAttributes.ProjectionExpression != null)
                        item = ItemOperations.ApplyProjection(item, keysAndAttributes.ProjectionExpression, keysAndAttributes.ExpressionAttributeNames);
                    tableItems.Add(item);
                }
            }

            responses[tableName] = tableItems;
        }

        return new BatchGetItemResponse
        {
            Responses = responses,
            UnprocessedKeys = new Dictionary<string, object>()
        };
    }

    public BatchWriteItemResponse BatchWriteItem(BatchWriteItemRequest request)
    {
        int totalRequests = 0;

        // First pass: validate
        foreach (var (tableName, writeRequests) in request.RequestItems)
        {
            tableStore.GetTable(tableName);

            var seenKeys = new HashSet<string>();

            foreach (var wr in writeRequests)
            {
                totalRequests++;
                if (totalRequests > 25)
                    throw new ValidationException("Too many items requested for the BatchWriteItem call");

                // Check for duplicate keys
                string keyStr;
                if (wr.PutRequest != null)
                {
                    var table = tableStore.GetTable(tableName);
                    var key = ItemOperations.ExtractKey(wr.PutRequest.Item, table);
                    keyStr = SerializeKey(key);
                }
                else if (wr.DeleteRequest != null)
                {
                    keyStr = SerializeKey(wr.DeleteRequest.Key);
                }
                else
                {
                    throw new ValidationException("Invalid write request");
                }

                if (!seenKeys.Add(keyStr))
                    throw new ValidationException("Provided list of item keys contains duplicates");
            }
        }

        // Second pass: execute
        foreach (var (tableName, writeRequests) in request.RequestItems)
        {
            foreach (var wr in writeRequests)
            {
                if (wr.PutRequest != null)
                {
                    itemStore.PutItem(tableName, wr.PutRequest.Item);
                }
                else if (wr.DeleteRequest != null)
                {
                    itemStore.DeleteItem(tableName, wr.DeleteRequest.Key);
                }
            }
        }

        return new BatchWriteItemResponse
        {
            UnprocessedItems = new Dictionary<string, object>()
        };
    }

    private static string SerializeKey(Dictionary<string, AttributeValue> key)
    {
        return JsonSerializer.Serialize(key, ItemOperations.JsonOptions);
    }
}
