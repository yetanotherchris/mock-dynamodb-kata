using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record KeySchemaElementDto
{
    [JsonPropertyName("AttributeName")]
    public required string AttributeName { get; init; }

    [JsonPropertyName("KeyType")]
    public required string KeyType { get; init; }
}

public sealed record AttributeDefinitionDto
{
    [JsonPropertyName("AttributeName")]
    public required string AttributeName { get; init; }

    [JsonPropertyName("AttributeType")]
    public required string AttributeType { get; init; }
}

public sealed record ProjectionDto
{
    [JsonPropertyName("ProjectionType")]
    public string ProjectionType { get; init; } = "ALL";

    [JsonPropertyName("NonKeyAttributes")]
    public List<string>? NonKeyAttributes { get; init; }
}

public sealed record ProvisionedThroughputDto
{
    [JsonPropertyName("ReadCapacityUnits")]
    public long ReadCapacityUnits { get; init; }

    [JsonPropertyName("WriteCapacityUnits")]
    public long WriteCapacityUnits { get; init; }

    [JsonPropertyName("NumberOfDecreasesToday")]
    public long? NumberOfDecreasesToday { get; init; }
}

public sealed record BillingModeSummaryDto
{
    [JsonPropertyName("BillingMode")]
    public string? BillingMode { get; init; }
}

public sealed record ConsumedCapacityDto
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("CapacityUnits")]
    public double CapacityUnits { get; init; }
}

public sealed record LocalSecondaryIndexDto
{
    [JsonPropertyName("IndexName")]
    public required string IndexName { get; init; }

    [JsonPropertyName("KeySchema")]
    public required List<KeySchemaElementDto> KeySchema { get; init; }

    [JsonPropertyName("Projection")]
    public required ProjectionDto Projection { get; init; }

    [JsonPropertyName("IndexArn")]
    public string? IndexArn { get; init; }

    [JsonPropertyName("IndexSizeBytes")]
    public long? IndexSizeBytes { get; init; }

    [JsonPropertyName("ItemCount")]
    public long? ItemCount { get; init; }
}

public sealed record GlobalSecondaryIndexDto
{
    [JsonPropertyName("IndexName")]
    public required string IndexName { get; init; }

    [JsonPropertyName("KeySchema")]
    public required List<KeySchemaElementDto> KeySchema { get; init; }

    [JsonPropertyName("Projection")]
    public required ProjectionDto Projection { get; init; }

    [JsonPropertyName("ProvisionedThroughput")]
    public ProvisionedThroughputDto? ProvisionedThroughput { get; init; }

    [JsonPropertyName("IndexArn")]
    public string? IndexArn { get; init; }

    [JsonPropertyName("IndexStatus")]
    public string? IndexStatus { get; init; }

    [JsonPropertyName("IndexSizeBytes")]
    public long? IndexSizeBytes { get; init; }

    [JsonPropertyName("ItemCount")]
    public long? ItemCount { get; init; }
}
