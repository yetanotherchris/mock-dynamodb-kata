using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record TableDescriptionDto
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("TableStatus")]
    public required string TableStatus { get; init; }

    [JsonPropertyName("TableArn")]
    public required string TableArn { get; init; }

    [JsonPropertyName("TableId")]
    public required string TableId { get; init; }

    [JsonPropertyName("CreationDateTime")]
    public required long CreationDateTime { get; init; }

    [JsonPropertyName("ItemCount")]
    public required long ItemCount { get; init; }

    [JsonPropertyName("TableSizeBytes")]
    public required long TableSizeBytes { get; init; }

    [JsonPropertyName("KeySchema")]
    public required List<KeySchemaElementDto> KeySchema { get; init; }

    [JsonPropertyName("AttributeDefinitions")]
    public required List<AttributeDefinitionDto> AttributeDefinitions { get; init; }

    [JsonPropertyName("ProvisionedThroughput")]
    public required ProvisionedThroughputDto ProvisionedThroughput { get; init; }

    [JsonPropertyName("BillingModeSummary")]
    public BillingModeSummaryDto? BillingModeSummary { get; init; }

    [JsonPropertyName("LocalSecondaryIndexes")]
    public List<LocalSecondaryIndexDto>? LocalSecondaryIndexes { get; init; }

    [JsonPropertyName("GlobalSecondaryIndexes")]
    public List<GlobalSecondaryIndexDto>? GlobalSecondaryIndexes { get; init; }
}

public sealed record CreateTableResponse
{
    [JsonPropertyName("TableDescription")]
    public required TableDescriptionDto TableDescription { get; init; }
}

public sealed record DeleteTableResponse
{
    [JsonPropertyName("TableDescription")]
    public required TableDescriptionDto TableDescription { get; init; }
}

public sealed record DescribeTableResponse
{
    [JsonPropertyName("Table")]
    public required TableDescriptionDto Table { get; init; }
}

public sealed record ListTablesResponse
{
    [JsonPropertyName("TableNames")]
    public required List<string> TableNames { get; init; }

    [JsonPropertyName("LastEvaluatedTableName")]
    public string? LastEvaluatedTableName { get; init; }
}
