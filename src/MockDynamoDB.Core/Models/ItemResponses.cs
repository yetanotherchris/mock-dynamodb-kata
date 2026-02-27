using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record PutItemResponse
{
    [JsonPropertyName("Attributes")]
    public Dictionary<string, AttributeValue>? Attributes { get; init; }
}

public sealed record GetItemResponse
{
    [JsonPropertyName("Item")]
    public Dictionary<string, AttributeValue>? Item { get; init; }
}

public sealed record DeleteItemResponse
{
    [JsonPropertyName("Attributes")]
    public Dictionary<string, AttributeValue>? Attributes { get; init; }
}

public sealed record UpdateItemResponse
{
    [JsonPropertyName("Attributes")]
    public Dictionary<string, AttributeValue>? Attributes { get; init; }
}
