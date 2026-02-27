using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record TransactWriteItemsResponse;

public sealed record TransactGetItemsResponse
{
    [JsonPropertyName("Responses")]
    public required List<ItemResponse> Responses { get; init; }
}

public sealed record ItemResponse
{
    [JsonPropertyName("Item")]
    public Dictionary<string, AttributeValue>? Item { get; init; }
}
