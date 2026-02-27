using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class TableOperations(ITableStore tableStore, IItemStore itemStore)
{
    public JsonDocument CreateTable(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var keySchema = ParseKeySchema(root.GetProperty("KeySchema"));
        var attrDefs = ParseAttributeDefinitions(root.GetProperty("AttributeDefinitions"));

        ValidateKeySchemaAttributes(keySchema, attrDefs);

        var table = new TableDefinition
        {
            TableName = tableName,
            KeySchema = keySchema,
            AttributeDefinitions = attrDefs,
            BillingMode = root.TryGetProperty("BillingMode", out var bm) ? bm.GetString() : "PAY_PER_REQUEST"
        };

        if (root.TryGetProperty("LocalSecondaryIndexes", out var lsiProp))
        {
            table.LocalSecondaryIndexes = ParseLocalSecondaryIndexes(lsiProp, table.HashKeyName, attrDefs);
        }

        if (root.TryGetProperty("GlobalSecondaryIndexes", out var gsiProp))
        {
            table.GlobalSecondaryIndexes = ParseGlobalSecondaryIndexes(gsiProp, attrDefs);
        }

        tableStore.CreateTable(table);
        itemStore.EnsureTable(tableName);

        return BuildTableDescriptionResponse("TableDescription", table);
    }

    public JsonDocument DeleteTable(JsonDocument request)
    {
        var tableName = request.RootElement.GetProperty("TableName").GetString()!;
        var table = tableStore.DeleteTable(tableName);
        itemStore.RemoveTable(tableName);
        table.TableStatus = "DELETING";
        return BuildTableDescriptionResponse("TableDescription", table);
    }

    public JsonDocument DescribeTable(JsonDocument request)
    {
        var tableName = request.RootElement.GetProperty("TableName").GetString()!;
        var table = tableStore.GetTable(tableName);
        return BuildTableDescriptionResponse("Table", table);
    }

    public JsonDocument ListTables(JsonDocument request)
    {
        var root = request.RootElement;
        var allNames = tableStore.ListTableNames();

        string? startTableName = null;
        int? limit = null;

        if (root.TryGetProperty("ExclusiveStartTableName", out var start))
            startTableName = start.GetString();
        if (root.TryGetProperty("Limit", out var lim))
            limit = lim.GetInt32();

        var filtered = allNames.AsEnumerable();
        if (startTableName != null)
            filtered = filtered.Where(n => string.Compare(n, startTableName, StringComparison.Ordinal) > 0);

        var names = filtered.ToList();
        string? lastEvaluated = null;

        if (limit.HasValue && names.Count > limit.Value)
        {
            names = names.Take(limit.Value).ToList();
            lastEvaluated = names.Last();
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WritePropertyName("TableNames");
        writer.WriteStartArray();
        foreach (var name in names)
            writer.WriteStringValue(name);
        writer.WriteEndArray();
        if (lastEvaluated != null)
            writer.WriteString("LastEvaluatedTableName", lastEvaluated);
        writer.WriteEndObject();
        writer.Flush();

        return JsonDocument.Parse(stream.ToArray());
    }

    private static List<KeySchemaElement> ParseKeySchema(JsonElement element)
    {
        var result = new List<KeySchemaElement>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(new KeySchemaElement
            {
                AttributeName = item.GetProperty("AttributeName").GetString()!,
                KeyType = item.GetProperty("KeyType").GetString()!
            });
        }
        return result;
    }

    private static List<AttributeDefinition> ParseAttributeDefinitions(JsonElement element)
    {
        var result = new List<AttributeDefinition>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(new AttributeDefinition
            {
                AttributeName = item.GetProperty("AttributeName").GetString()!,
                AttributeType = item.GetProperty("AttributeType").GetString()!
            });
        }
        return result;
    }

    private static List<LocalSecondaryIndexDefinition> ParseLocalSecondaryIndexes(
        JsonElement element, string tableHashKey, List<AttributeDefinition> attrDefs)
    {
        var indexes = new List<LocalSecondaryIndexDefinition>();

        foreach (var item in element.EnumerateArray())
        {
            var indexName = item.GetProperty("IndexName").GetString()!;
            var keySchema = ParseKeySchema(item.GetProperty("KeySchema"));
            var projection = ParseProjection(item.GetProperty("Projection"));

            var hashKey = keySchema.FirstOrDefault(k => k.KeyType == "HASH");
            if (hashKey == null || hashKey.AttributeName != tableHashKey)
                throw new ValidationException(
                    $"Table KeySchema: The AttributeValue for a key attribute cannot contain an empty string value. Index: {indexName}");

            var rangeKey = keySchema.FirstOrDefault(k => k.KeyType == "RANGE");
            if (rangeKey == null)
                throw new ValidationException($"Local Secondary Index {indexName} must have a RANGE key");

            if (!attrDefs.Any(a => a.AttributeName == rangeKey.AttributeName))
                throw new ValidationException(
                    $"One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            indexes.Add(new LocalSecondaryIndexDefinition
            {
                IndexName = indexName,
                KeySchema = keySchema,
                Projection = projection
            });
        }

        if (indexes.Count > 5)
            throw new ValidationException("One or more parameter values were invalid: Number of local secondary indexes exceeds limit of 5");

        return indexes;
    }

    private static List<GlobalSecondaryIndexDefinition> ParseGlobalSecondaryIndexes(
        JsonElement element, List<AttributeDefinition> attrDefs)
    {
        var indexes = new List<GlobalSecondaryIndexDefinition>();

        foreach (var item in element.EnumerateArray())
        {
            var indexName = item.GetProperty("IndexName").GetString()!;
            var keySchema = ParseKeySchema(item.GetProperty("KeySchema"));
            var projection = ParseProjection(item.GetProperty("Projection"));

            var hashKey = keySchema.FirstOrDefault(k => k.KeyType == "HASH");
            if (hashKey == null)
                throw new ValidationException($"Global Secondary Index {indexName} must have a HASH key");

            if (!attrDefs.Any(a => a.AttributeName == hashKey.AttributeName))
                throw new ValidationException(
                    "One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            var rangeKey = keySchema.FirstOrDefault(k => k.KeyType == "RANGE");
            if (rangeKey != null && !attrDefs.Any(a => a.AttributeName == rangeKey.AttributeName))
                throw new ValidationException(
                    "One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            indexes.Add(new GlobalSecondaryIndexDefinition
            {
                IndexName = indexName,
                KeySchema = keySchema,
                Projection = projection
            });
        }

        if (indexes.Count > 20)
            throw new ValidationException("One or more parameter values were invalid: Number of global secondary indexes exceeds limit of 20");

        return indexes;
    }

    private static ProjectionDefinition ParseProjection(JsonElement element)
    {
        return new ProjectionDefinition
        {
            ProjectionType = element.TryGetProperty("ProjectionType", out var pt) ? pt.GetString()! : "ALL",
            NonKeyAttributes = element.TryGetProperty("NonKeyAttributes", out var nka)
                ? nka.EnumerateArray().Select(e => e.GetString()!).ToList()
                : null
        };
    }

    private static void ValidateKeySchemaAttributes(List<KeySchemaElement> keySchema, List<AttributeDefinition> attrDefs)
    {
        foreach (var key in keySchema)
        {
            if (!attrDefs.Any(a => a.AttributeName == key.AttributeName))
                throw new ValidationException(
                    $"One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions. Keys: [{key.AttributeName}], AttributeDefinitions: [{string.Join(", ", attrDefs.Select(a => a.AttributeName))}]");
        }
    }

    private static JsonDocument BuildTableDescriptionResponse(string wrapperName, TableDefinition table)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WritePropertyName(wrapperName);
        WriteTableDescription(writer, table);
        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    private static void WriteTableDescription(Utf8JsonWriter writer, TableDefinition table)
    {
        writer.WriteStartObject();
        writer.WriteString("TableName", table.TableName);
        writer.WriteString("TableStatus", table.TableStatus);
        writer.WriteString("TableArn", table.TableArn);
        writer.WriteString("TableId", table.TableId);
        writer.WriteNumber("CreationDateTime", new DateTimeOffset(table.CreationDateTime).ToUnixTimeSeconds());
        writer.WriteNumber("ItemCount", table.ItemCount);
        writer.WriteNumber("TableSizeBytes", table.TableSizeBytes);

        writer.WritePropertyName("KeySchema");
        writer.WriteStartArray();
        foreach (var key in table.KeySchema)
        {
            writer.WriteStartObject();
            writer.WriteString("AttributeName", key.AttributeName);
            writer.WriteString("KeyType", key.KeyType);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("AttributeDefinitions");
        writer.WriteStartArray();
        foreach (var attr in table.AttributeDefinitions)
        {
            writer.WriteStartObject();
            writer.WriteString("AttributeName", attr.AttributeName);
            writer.WriteString("AttributeType", attr.AttributeType);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("ProvisionedThroughput");
        writer.WriteStartObject();
        writer.WriteNumber("ReadCapacityUnits", 0);
        writer.WriteNumber("WriteCapacityUnits", 0);
        writer.WriteNumber("NumberOfDecreasesToday", 0);
        writer.WriteEndObject();

        if (table.BillingMode != null)
        {
            writer.WritePropertyName("BillingModeSummary");
            writer.WriteStartObject();
            writer.WriteString("BillingMode", table.BillingMode);
            writer.WriteEndObject();
        }

        if (table.LocalSecondaryIndexes != null && table.LocalSecondaryIndexes.Count > 0)
        {
            writer.WritePropertyName("LocalSecondaryIndexes");
            writer.WriteStartArray();
            foreach (var lsi in table.LocalSecondaryIndexes)
            {
                writer.WriteStartObject();
                writer.WriteString("IndexName", lsi.IndexName);
                writer.WriteString("IndexArn", $"{table.TableArn}/index/{lsi.IndexName}");
                writer.WriteNumber("IndexSizeBytes", 0);
                writer.WriteNumber("ItemCount", 0);

                writer.WritePropertyName("KeySchema");
                writer.WriteStartArray();
                foreach (var key in lsi.KeySchema)
                {
                    writer.WriteStartObject();
                    writer.WriteString("AttributeName", key.AttributeName);
                    writer.WriteString("KeyType", key.KeyType);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WritePropertyName("Projection");
                writer.WriteStartObject();
                writer.WriteString("ProjectionType", lsi.Projection.ProjectionType);
                if (lsi.Projection.NonKeyAttributes != null)
                {
                    writer.WritePropertyName("NonKeyAttributes");
                    writer.WriteStartArray();
                    foreach (var attr in lsi.Projection.NonKeyAttributes)
                        writer.WriteStringValue(attr);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (table.GlobalSecondaryIndexes != null && table.GlobalSecondaryIndexes.Count > 0)
        {
            writer.WritePropertyName("GlobalSecondaryIndexes");
            writer.WriteStartArray();
            foreach (var gsi in table.GlobalSecondaryIndexes)
            {
                writer.WriteStartObject();
                writer.WriteString("IndexName", gsi.IndexName);
                writer.WriteString("IndexArn", $"{table.TableArn}/index/{gsi.IndexName}");
                writer.WriteString("IndexStatus", "ACTIVE");
                writer.WriteNumber("IndexSizeBytes", 0);
                writer.WriteNumber("ItemCount", 0);

                writer.WritePropertyName("KeySchema");
                writer.WriteStartArray();
                foreach (var key in gsi.KeySchema)
                {
                    writer.WriteStartObject();
                    writer.WriteString("AttributeName", key.AttributeName);
                    writer.WriteString("KeyType", key.KeyType);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WritePropertyName("Projection");
                writer.WriteStartObject();
                writer.WriteString("ProjectionType", gsi.Projection.ProjectionType);
                if (gsi.Projection.NonKeyAttributes != null)
                {
                    writer.WritePropertyName("NonKeyAttributes");
                    writer.WriteStartArray();
                    foreach (var attr in gsi.Projection.NonKeyAttributes)
                        writer.WriteStringValue(attr);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
