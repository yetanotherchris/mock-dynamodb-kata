using System.Text.Json;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DynamoDbException ex)
        {
            await WriteError(context, ex.StatusCode, ex.ErrorType, ex.Message, ex);
        }
        catch (JsonException)
        {
            await WriteError(context, 400,
                "com.amazonaws.dynamodb.v20120810#SerializationException",
                "Start of structure or map found where not expected");
        }
    }

    private static async Task WriteError(
        HttpContext context, int statusCode, string errorType, string message,
        DynamoDbException? ex = null)
    {
        context.Response.ContentType = "application/x-amz-json-1.0";
        context.Response.StatusCode = statusCode;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("__type", errorType);
            writer.WriteString("Message", message);
            if (ex is TransactionCanceledException txEx)
            {
                writer.WritePropertyName("CancellationReasons");
                JsonSerializer.Serialize(writer, txEx.CancellationReasons);
            }
            writer.WriteEndObject();
        }
        await context.Response.Body.WriteAsync(stream.ToArray());
    }
}
