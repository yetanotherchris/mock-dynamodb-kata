using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class BatchOperations(ITableStore tableStore, IItemStore itemStore)
{
    public JsonDocument BatchGetItem(JsonDocument request)
    {
        var root = request.RootElement;
        var requestItems = root.GetProperty("RequestItems");

        int totalKeys = 0;
        var responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>();

        foreach (var tableProp in requestItems.EnumerateObject())
        {
            var tableName = tableProp.Name;
            tableStore.GetTable(tableName); // validate table exists

            var keysAndAttributes = tableProp.Value;
            var keys = keysAndAttributes.GetProperty("Keys");

            string? projectionExpression = null;
            Dictionary<string, string>? expressionAttributeNames = null;

            if (keysAndAttributes.TryGetProperty("ProjectionExpression", out var pe))
                projectionExpression = pe.GetString();
            if (keysAndAttributes.TryGetProperty("ExpressionAttributeNames", out var ean))
                expressionAttributeNames = ItemOperations.DeserializeStringMap(ean);

            var tableItems = new List<Dictionary<string, AttributeValue>>();

            foreach (var keyElement in keys.EnumerateArray())
            {
                totalKeys++;
                if (totalKeys > 100)
                    throw new ValidationException("Too many items requested for the BatchGetItem call");

                var key = ItemOperations.DeserializeItem(keyElement);
                var item = itemStore.GetItem(tableName, key);
                if (item != null)
                {
                    if (projectionExpression != null)
                        item = ItemOperations.ApplyProjection(item, projectionExpression, expressionAttributeNames);
                    tableItems.Add(item);
                }
            }

            responses[tableName] = tableItems;
        }

        return BuildBatchGetResponse(responses);
    }

    public JsonDocument BatchWriteItem(JsonDocument request)
    {
        var root = request.RootElement;
        var requestItems = root.GetProperty("RequestItems");

        int totalRequests = 0;

        // First pass: validate
        foreach (var tableProp in requestItems.EnumerateObject())
        {
            var tableName = tableProp.Name;
            tableStore.GetTable(tableName);

            var writeRequests = tableProp.Value;
            var seenKeys = new HashSet<string>();

            foreach (var wr in writeRequests.EnumerateArray())
            {
                totalRequests++;
                if (totalRequests > 25)
                    throw new ValidationException("Too many items requested for the BatchWriteItem call");

                // Check for duplicate keys
                string keyStr;
                if (wr.TryGetProperty("PutRequest", out var put))
                {
                    var item = ItemOperations.DeserializeItem(put.GetProperty("Item"));
                    var table = tableStore.GetTable(tableName);
                    var key = ItemOperations.ExtractKey(item, table);
                    keyStr = SerializeKey(key);
                }
                else if (wr.TryGetProperty("DeleteRequest", out var del))
                {
                    var key = ItemOperations.DeserializeItem(del.GetProperty("Key"));
                    keyStr = SerializeKey(key);
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
        foreach (var tableProp in requestItems.EnumerateObject())
        {
            var tableName = tableProp.Name;
            var table = tableStore.GetTable(tableName);

            foreach (var wr in tableProp.Value.EnumerateArray())
            {
                if (wr.TryGetProperty("PutRequest", out var put))
                {
                    var item = ItemOperations.DeserializeItem(put.GetProperty("Item"));
                    itemStore.PutItem(tableName, item);
                }
                else if (wr.TryGetProperty("DeleteRequest", out var del))
                {
                    var key = ItemOperations.DeserializeItem(del.GetProperty("Key"));
                    itemStore.DeleteItem(tableName, key);
                }
            }
        }

        return BuildBatchWriteResponse();
    }

    private static string SerializeKey(Dictionary<string, AttributeValue> key)
    {
        return JsonSerializer.Serialize(key, ItemOperations.JsonOptions);
    }

    private static JsonDocument BuildBatchGetResponse(Dictionary<string, List<Dictionary<string, AttributeValue>>> responses)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        writer.WritePropertyName("Responses");
        writer.WriteStartObject();
        foreach (var (tableName, items) in responses)
        {
            writer.WritePropertyName(tableName);
            ItemOperations.WriteItemsList(writer, items);
        }
        writer.WriteEndObject();

        writer.WritePropertyName("UnprocessedKeys");
        writer.WriteStartObject();
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    private static JsonDocument BuildBatchWriteResponse()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        writer.WritePropertyName("UnprocessedItems");
        writer.WriteStartObject();
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }
}
