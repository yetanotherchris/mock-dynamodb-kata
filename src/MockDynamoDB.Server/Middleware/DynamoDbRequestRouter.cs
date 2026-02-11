using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Middleware;

public class DynamoDbRequestRouter
{
    private const string TargetPrefix = "DynamoDB_20120810.";
    private readonly TableOperations _tableOps;
    private readonly ItemOperations _itemOps;

    public DynamoDbRequestRouter(TableOperations tableOps, ItemOperations itemOps)
    {
        _tableOps = tableOps;
        _itemOps = itemOps;
    }

    public async Task HandleRequest(HttpContext context)
    {
        if (context.Request.Method == "GET" && context.Request.Path == "/")
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
            return;
        }

        if (context.Request.Method != "POST" || context.Request.Path != "/")
        {
            context.Response.StatusCode = 404;
            return;
        }

        var target = context.Request.Headers["X-Amz-Target"].FirstOrDefault();
        if (string.IsNullOrEmpty(target))
        {
            await WriteError(context, 400,
                "com.amazonaws.dynamodb.v20120810#MissingAuthenticationTokenException",
                "Missing Authentication Token");
            return;
        }

        if (!target.StartsWith(TargetPrefix))
        {
            await WriteError(context, 400,
                "com.amazonaws.dynamodb.v20120810#UnknownOperationException", "");
            return;
        }

        var operation = target[TargetPrefix.Length..];

        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body);
            var result = DispatchOperation(operation, body);

            context.Response.ContentType = "application/x-amz-json-1.0";
            context.Response.StatusCode = 200;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                result.RootElement.WriteTo(writer);
            }
            await context.Response.Body.WriteAsync(stream.ToArray());
            result.Dispose();
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

    private JsonDocument DispatchOperation(string operation, JsonDocument body)
    {
        return operation switch
        {
            "CreateTable" => _tableOps.CreateTable(body),
            "DeleteTable" => _tableOps.DeleteTable(body),
            "DescribeTable" => _tableOps.DescribeTable(body),
            "ListTables" => _tableOps.ListTables(body),
            "PutItem" => _itemOps.PutItem(body),
            "GetItem" => _itemOps.GetItem(body),
            "DeleteItem" => _itemOps.DeleteItem(body),
            _ => throw new UnknownOperationException()
        };
    }

    private static async Task WriteError(HttpContext context, int statusCode, string errorType, string message, DynamoDbException? ex = null)
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
