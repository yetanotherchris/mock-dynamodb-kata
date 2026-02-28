using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record BatchGetItemResponse
{
    [JsonPropertyName("Responses")]
    public required Dictionary<string, List<Dictionary<string, AttributeValue>>> Responses { get; init; }

    [JsonPropertyName("UnprocessedKeys")]
    public required Dictionary<string, object> UnprocessedKeys { get; init; }
}

public sealed record BatchWriteItemResponse
{
    [JsonPropertyName("UnprocessedItems")]
    public required Dictionary<string, object> UnprocessedItems { get; init; }
}
