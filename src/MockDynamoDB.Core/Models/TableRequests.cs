using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record CreateTableRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("KeySchema")]
    public required List<KeySchemaElementDto> KeySchema { get; init; }

    [JsonPropertyName("AttributeDefinitions")]
    public required List<AttributeDefinitionDto> AttributeDefinitions { get; init; }

    [JsonPropertyName("BillingMode")]
    public string? BillingMode { get; init; }

    [JsonPropertyName("LocalSecondaryIndexes")]
    public List<LocalSecondaryIndexDto>? LocalSecondaryIndexes { get; init; }

    [JsonPropertyName("GlobalSecondaryIndexes")]
    public List<GlobalSecondaryIndexDto>? GlobalSecondaryIndexes { get; init; }
}

public sealed record DeleteTableRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }
}

public sealed record DescribeTableRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }
}

public sealed record ListTablesRequest
{
    [JsonPropertyName("ExclusiveStartTableName")]
    public string? ExclusiveStartTableName { get; init; }

    [JsonPropertyName("Limit")]
    public int? Limit { get; init; }
}
