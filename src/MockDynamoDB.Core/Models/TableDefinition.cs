namespace MockDynamoDB.Core.Models;

public class TableDefinition
{
    public required string TableName { get; set; }
    public required List<KeySchemaElement> KeySchema { get; set; }
    public required List<AttributeDefinition> AttributeDefinitions { get; set; }
    public List<LocalSecondaryIndexDefinition>? LocalSecondaryIndexes { get; set; }
    public List<GlobalSecondaryIndexDefinition>? GlobalSecondaryIndexes { get; set; }
    public string TableStatus { get; set; } = "ACTIVE";
    public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;
    public string TableId { get; set; } = Guid.NewGuid().ToString();
    public long ItemCount { get; set; }
    public long TableSizeBytes { get; set; }
    public string? BillingMode { get; set; }

    public string HashKeyName => KeySchema.First(k => k.KeyType == "HASH").AttributeName;
    public string? RangeKeyName => KeySchema.FirstOrDefault(k => k.KeyType == "RANGE")?.AttributeName;
    public bool HasRangeKey => RangeKeyName != null;

    public string TableArn => $"arn:aws:dynamodb:us-east-1:000000000000:table/{TableName}";
}

public class KeySchemaElement
{
    public required string AttributeName { get; set; }
    public required string KeyType { get; set; }
}

public class AttributeDefinition
{
    public required string AttributeName { get; set; }
    public required string AttributeType { get; set; }
}

public class LocalSecondaryIndexDefinition
{
    public required string IndexName { get; set; }
    public required List<KeySchemaElement> KeySchema { get; set; }
    public required ProjectionDefinition Projection { get; set; }

    public string HashKeyName => KeySchema.First(k => k.KeyType == "HASH").AttributeName;
    public string RangeKeyName => KeySchema.First(k => k.KeyType == "RANGE").AttributeName;
}

public class GlobalSecondaryIndexDefinition
{
    public required string IndexName { get; set; }
    public required List<KeySchemaElement> KeySchema { get; set; }
    public required ProjectionDefinition Projection { get; set; }

    public string HashKeyName => KeySchema.First(k => k.KeyType == "HASH").AttributeName;
    public string? RangeKeyName => KeySchema.FirstOrDefault(k => k.KeyType == "RANGE")?.AttributeName;
}

public class ProjectionDefinition
{
    public string ProjectionType { get; set; } = "ALL";
    public List<string>? NonKeyAttributes { get; set; }
}
