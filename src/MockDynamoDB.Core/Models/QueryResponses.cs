using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record QueryResponse
{
    [JsonPropertyName("Items")]
    public List<Dictionary<string, AttributeValue>>? Items { get; init; }

    [JsonPropertyName("Count")]
    public required int Count { get; init; }

    [JsonPropertyName("ScannedCount")]
    public required int ScannedCount { get; init; }

    [JsonPropertyName("LastEvaluatedKey")]
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; init; }

    [JsonPropertyName("ConsumedCapacity")]
    public ConsumedCapacityDto? ConsumedCapacity { get; init; }
}

public sealed record ScanResponse
{
    [JsonPropertyName("Items")]
    public List<Dictionary<string, AttributeValue>>? Items { get; init; }

    [JsonPropertyName("Count")]
    public required int Count { get; init; }

    [JsonPropertyName("ScannedCount")]
    public required int ScannedCount { get; init; }

    [JsonPropertyName("LastEvaluatedKey")]
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; init; }
}
