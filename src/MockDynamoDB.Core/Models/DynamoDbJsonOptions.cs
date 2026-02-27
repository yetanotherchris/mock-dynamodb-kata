using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

public static class DynamoDbJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new AttributeValueConverter() }
    };
}
