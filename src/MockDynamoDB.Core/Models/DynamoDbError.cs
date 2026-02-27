using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public class DynamoDbException : Exception
{
    public string ErrorType { get; }
    public int StatusCode { get; }

    public DynamoDbException(string errorType, string message, int statusCode = 400)
        : base(message)
    {
        ErrorType = errorType;
        StatusCode = statusCode;
    }
}

public class ResourceNotFoundException : DynamoDbException
{
    public ResourceNotFoundException(string message = "Requested resource not found")
        : base("com.amazonaws.dynamodb.v20120810#ResourceNotFoundException", message) { }
}

public class ResourceInUseException : DynamoDbException
{
    public ResourceInUseException(string message = "Table already exists")
        : base("com.amazonaws.dynamodb.v20120810#ResourceInUseException", message) { }
}

public class ValidationException : DynamoDbException
{
    public ValidationException(string message)
        : base("com.amazonaws.dynamodb.v20120810#ValidationException", message) { }
}

public class ConditionalCheckFailedException : DynamoDbException
{
    public ConditionalCheckFailedException(string message = "The conditional request failed")
        : base("com.amazonaws.dynamodb.v20120810#ConditionalCheckFailedException", message) { }
}

public class TransactionCanceledException : DynamoDbException
{
    public List<CancellationReason> CancellationReasons { get; }

    public TransactionCanceledException(List<CancellationReason> reasons)
        : base("com.amazonaws.dynamodb.v20120810#TransactionCanceledException",
               "Transaction cancelled, please refer cancellation reasons for specific reasons [" +
               string.Join(", ", reasons.Select(r => r.Code ?? "None")) + "]")
    {
        CancellationReasons = reasons;
    }
}

public sealed record CancellationReason
{
    [JsonPropertyName("Code")]
    public string? Code { get; init; }

    [JsonPropertyName("Message")]
    public string? Message { get; init; }

    [JsonPropertyName("Item")]
    public Dictionary<string, AttributeValue>? Item { get; init; }
}

public class UnknownOperationException : DynamoDbException
{
    public UnknownOperationException()
        : base("com.amazonaws.dynamodb.v20120810#UnknownOperationException", "") { }
}
