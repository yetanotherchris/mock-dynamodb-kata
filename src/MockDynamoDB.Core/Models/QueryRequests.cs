using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record QueryRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("IndexName")]
    public string? IndexName { get; init; }

    [JsonPropertyName("KeyConditionExpression")]
    public string? KeyConditionExpression { get; init; }

    [JsonPropertyName("KeyConditions")]
    public JsonElement? KeyConditions { get; init; }

    [JsonPropertyName("FilterExpression")]
    public string? FilterExpression { get; init; }

    [JsonPropertyName("QueryFilter")]
    public JsonElement? QueryFilter { get; init; }

    [JsonPropertyName("ConditionalOperator")]
    public string? ConditionalOperator { get; init; }

    [JsonPropertyName("ProjectionExpression")]
    public string? ProjectionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("ScanIndexForward")]
    public bool? ScanIndexForward { get; init; }

    [JsonPropertyName("Limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("ExclusiveStartKey")]
    public Dictionary<string, AttributeValue>? ExclusiveStartKey { get; init; }

    [JsonPropertyName("Select")]
    public string? Select { get; init; }

    [JsonPropertyName("ReturnConsumedCapacity")]
    public string? ReturnConsumedCapacity { get; init; }
}

public sealed record ScanRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("FilterExpression")]
    public string? FilterExpression { get; init; }

    [JsonPropertyName("ScanFilter")]
    public JsonElement? ScanFilter { get; init; }

    [JsonPropertyName("ConditionalOperator")]
    public string? ConditionalOperator { get; init; }

    [JsonPropertyName("ProjectionExpression")]
    public string? ProjectionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("Limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("ExclusiveStartKey")]
    public Dictionary<string, AttributeValue>? ExclusiveStartKey { get; init; }

    [JsonPropertyName("Select")]
    public string? Select { get; init; }

    [JsonPropertyName("TotalSegments")]
    public int? TotalSegments { get; init; }

    [JsonPropertyName("Segment")]
    public int? Segment { get; init; }
}
