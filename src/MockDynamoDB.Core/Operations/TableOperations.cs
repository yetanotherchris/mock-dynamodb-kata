using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class TableOperations(ITableStore tableStore, IItemStore itemStore)
{
    public CreateTableResponse CreateTable(CreateTableRequest request)
    {
        var keySchema = request.KeySchema.Select(k => new KeySchemaElement
        {
            AttributeName = k.AttributeName,
            KeyType = k.KeyType
        }).ToList();

        var attrDefs = request.AttributeDefinitions.Select(a => new AttributeDefinition
        {
            AttributeName = a.AttributeName,
            AttributeType = a.AttributeType
        }).ToList();

        ValidateKeySchemaAttributes(keySchema, attrDefs);

        var table = new TableDefinition
        {
            TableName = request.TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attrDefs,
            BillingMode = request.BillingMode ?? "PAY_PER_REQUEST"
        };

        if (request.LocalSecondaryIndexes != null)
        {
            table.LocalSecondaryIndexes = ParseLocalSecondaryIndexes(request.LocalSecondaryIndexes, table.HashKeyName, attrDefs);
        }

        if (request.GlobalSecondaryIndexes != null)
        {
            table.GlobalSecondaryIndexes = ParseGlobalSecondaryIndexes(request.GlobalSecondaryIndexes, attrDefs);
        }

        tableStore.CreateTable(table);
        itemStore.EnsureTable(request.TableName);

        return new CreateTableResponse { TableDescription = MapTableDescription(table) };
    }

    public DeleteTableResponse DeleteTable(DeleteTableRequest request)
    {
        var table = tableStore.DeleteTable(request.TableName);
        itemStore.RemoveTable(request.TableName);
        table.TableStatus = "DELETING";
        return new DeleteTableResponse { TableDescription = MapTableDescription(table) };
    }

    public DescribeTableResponse DescribeTable(DescribeTableRequest request)
    {
        var table = tableStore.GetTable(request.TableName);
        return new DescribeTableResponse { Table = MapTableDescription(table) };
    }

    public ListTablesResponse ListTables(ListTablesRequest request)
    {
        var allNames = tableStore.ListTableNames();

        var filtered = allNames.AsEnumerable();
        if (request.ExclusiveStartTableName != null)
            filtered = filtered.Where(n => string.Compare(n, request.ExclusiveStartTableName, StringComparison.Ordinal) > 0);

        var names = filtered.ToList();
        string? lastEvaluated = null;

        if (request.Limit.HasValue && names.Count > request.Limit.Value)
        {
            names = names.Take(request.Limit.Value).ToList();
            lastEvaluated = names.Last();
        }

        return new ListTablesResponse
        {
            TableNames = names,
            LastEvaluatedTableName = lastEvaluated
        };
    }

    internal static TableDescriptionDto MapTableDescription(TableDefinition table)
    {
        return new TableDescriptionDto
        {
            TableName = table.TableName,
            TableStatus = table.TableStatus,
            TableArn = table.TableArn,
            TableId = table.TableId,
            CreationDateTime = new DateTimeOffset(table.CreationDateTime).ToUnixTimeSeconds(),
            ItemCount = table.ItemCount,
            TableSizeBytes = table.TableSizeBytes,
            KeySchema = table.KeySchema.Select(k => new KeySchemaElementDto
            {
                AttributeName = k.AttributeName,
                KeyType = k.KeyType
            }).ToList(),
            AttributeDefinitions = table.AttributeDefinitions.Select(a => new AttributeDefinitionDto
            {
                AttributeName = a.AttributeName,
                AttributeType = a.AttributeType
            }).ToList(),
            ProvisionedThroughput = new ProvisionedThroughputDto
            {
                ReadCapacityUnits = 0,
                WriteCapacityUnits = 0,
                NumberOfDecreasesToday = 0
            },
            BillingModeSummary = table.BillingMode != null
                ? new BillingModeSummaryDto { BillingMode = table.BillingMode }
                : null,
            LocalSecondaryIndexes = table.LocalSecondaryIndexes is { Count: > 0 }
                ? table.LocalSecondaryIndexes.Select(lsi => new LocalSecondaryIndexDto
                {
                    IndexName = lsi.IndexName,
                    IndexArn = $"{table.TableArn}/index/{lsi.IndexName}",
                    IndexSizeBytes = 0,
                    ItemCount = 0,
                    KeySchema = lsi.KeySchema.Select(k => new KeySchemaElementDto
                    {
                        AttributeName = k.AttributeName,
                        KeyType = k.KeyType
                    }).ToList(),
                    Projection = new ProjectionDto
                    {
                        ProjectionType = lsi.Projection.ProjectionType,
                        NonKeyAttributes = lsi.Projection.NonKeyAttributes
                    }
                }).ToList()
                : null,
            GlobalSecondaryIndexes = table.GlobalSecondaryIndexes is { Count: > 0 }
                ? table.GlobalSecondaryIndexes.Select(gsi => new GlobalSecondaryIndexDto
                {
                    IndexName = gsi.IndexName,
                    IndexArn = $"{table.TableArn}/index/{gsi.IndexName}",
                    IndexStatus = "ACTIVE",
                    IndexSizeBytes = 0,
                    ItemCount = 0,
                    KeySchema = gsi.KeySchema.Select(k => new KeySchemaElementDto
                    {
                        AttributeName = k.AttributeName,
                        KeyType = k.KeyType
                    }).ToList(),
                    Projection = new ProjectionDto
                    {
                        ProjectionType = gsi.Projection.ProjectionType,
                        NonKeyAttributes = gsi.Projection.NonKeyAttributes
                    }
                }).ToList()
                : null
        };
    }

    private static List<LocalSecondaryIndexDefinition> ParseLocalSecondaryIndexes(
        List<LocalSecondaryIndexDto> indexes, string tableHashKey, List<AttributeDefinition> attrDefs)
    {
        var result = new List<LocalSecondaryIndexDefinition>();

        foreach (var idx in indexes)
        {
            var keySchema = idx.KeySchema.Select(k => new KeySchemaElement
            {
                AttributeName = k.AttributeName,
                KeyType = k.KeyType
            }).ToList();

            var hashKey = keySchema.FirstOrDefault(k => k.KeyType == "HASH");
            if (hashKey == null || hashKey.AttributeName != tableHashKey)
                throw new ValidationException(
                    $"Table KeySchema: The AttributeValue for a key attribute cannot contain an empty string value. Index: {idx.IndexName}");

            var rangeKey = keySchema.FirstOrDefault(k => k.KeyType == "RANGE");
            if (rangeKey == null)
                throw new ValidationException($"Local Secondary Index {idx.IndexName} must have a RANGE key");

            if (!attrDefs.Any(a => a.AttributeName == rangeKey.AttributeName))
                throw new ValidationException(
                    $"One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            result.Add(new LocalSecondaryIndexDefinition
            {
                IndexName = idx.IndexName,
                KeySchema = keySchema,
                Projection = new ProjectionDefinition
                {
                    ProjectionType = idx.Projection.ProjectionType,
                    NonKeyAttributes = idx.Projection.NonKeyAttributes
                }
            });
        }

        if (result.Count > 5)
            throw new ValidationException("One or more parameter values were invalid: Number of local secondary indexes exceeds limit of 5");

        return result;
    }

    private static List<GlobalSecondaryIndexDefinition> ParseGlobalSecondaryIndexes(
        List<GlobalSecondaryIndexDto> indexes, List<AttributeDefinition> attrDefs)
    {
        var result = new List<GlobalSecondaryIndexDefinition>();

        foreach (var idx in indexes)
        {
            var keySchema = idx.KeySchema.Select(k => new KeySchemaElement
            {
                AttributeName = k.AttributeName,
                KeyType = k.KeyType
            }).ToList();

            var hashKey = keySchema.FirstOrDefault(k => k.KeyType == "HASH");
            if (hashKey == null)
                throw new ValidationException($"Global Secondary Index {idx.IndexName} must have a HASH key");

            if (!attrDefs.Any(a => a.AttributeName == hashKey.AttributeName))
                throw new ValidationException(
                    "One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            var rangeKey = keySchema.FirstOrDefault(k => k.KeyType == "RANGE");
            if (rangeKey != null && !attrDefs.Any(a => a.AttributeName == rangeKey.AttributeName))
                throw new ValidationException(
                    "One or more parameter values were invalid: Some index key attributes are not defined in AttributeDefinitions.");

            result.Add(new GlobalSecondaryIndexDefinition
            {
                IndexName = idx.IndexName,
                KeySchema = keySchema,
                Projection = new ProjectionDefinition
                {
                    ProjectionType = idx.Projection.ProjectionType,
                    NonKeyAttributes = idx.Projection.NonKeyAttributes
                }
            });
        }

        if (result.Count > 20)
            throw new ValidationException("One or more parameter values were invalid: Number of global secondary indexes exceeds limit of 20");

        return result;
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
}
