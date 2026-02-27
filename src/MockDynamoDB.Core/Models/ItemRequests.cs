using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record PutItemRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Item")]
    public required Dictionary<string, AttributeValue> Item { get; init; }

    [JsonPropertyName("ReturnValues")]
    public string? ReturnValues { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("Expected")]
    public JsonElement? Expected { get; init; }

    [JsonPropertyName("ConditionalOperator")]
    public string? ConditionalOperator { get; init; }
}

public sealed record GetItemRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("ProjectionExpression")]
    public string? ProjectionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }
}

public sealed record DeleteItemRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("ReturnValues")]
    public string? ReturnValues { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("Expected")]
    public JsonElement? Expected { get; init; }

    [JsonPropertyName("ConditionalOperator")]
    public string? ConditionalOperator { get; init; }
}

public sealed record UpdateItemRequest
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("ReturnValues")]
    public string? ReturnValues { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("UpdateExpression")]
    public string? UpdateExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }

    [JsonPropertyName("Expected")]
    public JsonElement? Expected { get; init; }

    [JsonPropertyName("ConditionalOperator")]
    public string? ConditionalOperator { get; init; }

    [JsonPropertyName("AttributeUpdates")]
    public JsonElement? AttributeUpdates { get; init; }
}
