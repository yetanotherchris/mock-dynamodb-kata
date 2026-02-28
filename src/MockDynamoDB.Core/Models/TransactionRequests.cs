using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public sealed record TransactWriteItemsRequest
{
    [JsonPropertyName("TransactItems")]
    public required List<TransactWriteItem> TransactItems { get; init; }
}

public sealed record TransactWriteItem
{
    [JsonPropertyName("Put")]
    public TransactPut? Put { get; init; }

    [JsonPropertyName("Delete")]
    public TransactDelete? Delete { get; init; }

    [JsonPropertyName("Update")]
    public TransactUpdate? Update { get; init; }

    [JsonPropertyName("ConditionCheck")]
    public TransactConditionCheck? ConditionCheck { get; init; }
}

public sealed record TransactPut
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Item")]
    public required Dictionary<string, AttributeValue> Item { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }
}

public sealed record TransactDelete
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }
}

public sealed record TransactUpdate
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("UpdateExpression")]
    public string? UpdateExpression { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public string? ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }
}

public sealed record TransactConditionCheck
{
    [JsonPropertyName("TableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("Key")]
    public required Dictionary<string, AttributeValue> Key { get; init; }

    [JsonPropertyName("ConditionExpression")]
    public required string ConditionExpression { get; init; }

    [JsonPropertyName("ExpressionAttributeNames")]
    public Dictionary<string, string>? ExpressionAttributeNames { get; init; }

    [JsonPropertyName("ExpressionAttributeValues")]
    public Dictionary<string, AttributeValue>? ExpressionAttributeValues { get; init; }
}

public sealed record TransactGetItemsRequest
{
    [JsonPropertyName("TransactItems")]
    public required List<TransactGetItem> TransactItems { get; init; }
}

public sealed record TransactGetItem
{
    [JsonPropertyName("Get")]
    public required TransactGet Get { get; init; }
}

public sealed record TransactGet
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
