using System.Text.Json;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Server.Commands;

namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbRequestRouter
{
    private const string TargetPrefix = "DynamoDB_20120810.";
    private static readonly JsonSerializerOptions JsonOptions = DynamoDbJsonOptions.Options;
    private readonly Dictionary<string, IDynamoDbCommand> _commands;

    public DynamoDbRequestRouter(IEnumerable<IDynamoDbCommand> commands)
    {
        _commands = commands.ToDictionary(c => c.OperationName);
    }

    public async Task HandleRequest(HttpContext context)
    {
        var operation = context.Request.Headers["X-Amz-Target"].FirstOrDefault()![TargetPrefix.Length..];

        if (!_commands.TryGetValue(operation, out var command))
            throw new UnknownOperationException();

        var result = await command.HandleAsync(context.Request.Body, JsonOptions);
        context.Response.ContentType = "application/x-amz-json-1.0";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(result);
    }
}
