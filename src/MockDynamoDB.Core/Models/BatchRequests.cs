using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record BatchGetItemRequest
{
    [JsonPropertyName("RequestItems")]
    public required Dictionary<string, BatchGetTableRequest> RequestItems { get; init; }
}

public sealed record BatchGetTableRequest
{
    [JsonPropertyName("Keys")]
    public required List<Dictionary<string, AttributeValue>> Keys { get; init; }

    [JsonPropertyName("ProjectionExpression")]
    public string? ProjectionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }
}

public sealed record BatchWriteItemRequest
{
    [JsonPropertyName("RequestItems")]
    public required Dictionary<string, List<WriteRequest>> RequestItems { get; init; }
}

public sealed record WriteRequest
{
    [JsonPropertyName("PutRequest")]
    public BatchPutRequest? PutRequest { get; init; }

    [JsonPropertyName("DeleteRequest")]
    public BatchDeleteRequest? DeleteRequest { get; init; }
}

public sealed record BatchPutRequest
{
    [JsonPropertyName("Item")]
    public required Dictionary<string, AttributeValue> Item { get; init; }
}

public sealed record BatchDeleteRequest
{
    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }
}
