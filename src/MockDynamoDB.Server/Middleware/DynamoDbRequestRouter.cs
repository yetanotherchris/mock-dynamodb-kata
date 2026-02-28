using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbRequestRouter(
    TableOperations tableOps,
    ItemOperations itemOps,
    QueryScanOperations queryScanOps,
    BatchOperations batchOps,
    TransactionOperations txOps)
{
    private const string TargetPrefix = "DynamoDB_20120810.";
    private static readonly JsonSerializerOptions JsonOptions = DynamoDbJsonOptions.Options;

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
            var result = operation switch
            {
                "CreateTable" => await Dispatch<CreateTableRequest, CreateTableResponse>(context.Request.Body, tableOps.CreateTable),
                "DeleteTable" => await Dispatch<DeleteTableRequest, DeleteTableResponse>(context.Request.Body, tableOps.DeleteTable),
                "DescribeTable" => await Dispatch<DescribeTableRequest, DescribeTableResponse>(context.Request.Body, tableOps.DescribeTable),
                "ListTables" => await Dispatch<ListTablesRequest, ListTablesResponse>(context.Request.Body, tableOps.ListTables),
                "PutItem" => await Dispatch<PutItemRequest, PutItemResponse>(context.Request.Body, itemOps.PutItem),
                "GetItem" => await Dispatch<GetItemRequest, GetItemResponse>(context.Request.Body, itemOps.GetItem),
                "DeleteItem" => await Dispatch<DeleteItemRequest, DeleteItemResponse>(context.Request.Body, itemOps.DeleteItem),
                "UpdateItem" => await Dispatch<UpdateItemRequest, UpdateItemResponse>(context.Request.Body, itemOps.UpdateItem),
                "Query" => await Dispatch<QueryRequest, QueryResponse>(context.Request.Body, queryScanOps.Query),
                "Scan" => await Dispatch<ScanRequest, ScanResponse>(context.Request.Body, queryScanOps.Scan),
                "BatchGetItem" => await Dispatch<BatchGetItemRequest, BatchGetItemResponse>(context.Request.Body, batchOps.BatchGetItem),
                "BatchWriteItem" => await Dispatch<BatchWriteItemRequest, BatchWriteItemResponse>(context.Request.Body, batchOps.BatchWriteItem),
                "TransactWriteItems" => await Dispatch<TransactWriteItemsRequest, TransactWriteItemsResponse>(context.Request.Body, txOps.TransactWriteItems),
                "TransactGetItems" => await Dispatch<TransactGetItemsRequest, TransactGetItemsResponse>(context.Request.Body, txOps.TransactGetItems),
                _ => throw new UnknownOperationException()
            };

            context.Response.ContentType = "application/x-amz-json-1.0";
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(result);
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

    private static async Task<byte[]> Dispatch<TReq, TRes>(Stream body, Func<TReq, TRes> handler)
    {
        var request = await JsonSerializer.DeserializeAsync<TReq>(body, JsonOptions);
        var response = handler(request!);
        return JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
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
