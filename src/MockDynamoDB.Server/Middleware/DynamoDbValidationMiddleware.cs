using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Server.Middleware;

public sealed class DynamoDbValidationMiddleware(RequestDelegate next)
{
    private const string TargetPrefix = "DynamoDB_20120810.";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != "POST" || context.Request.Path != "/")
        {
            context.Response.StatusCode = 404;
            return;
        }

        var target = context.Request.Headers["X-Amz-Target"].FirstOrDefault();
        if (string.IsNullOrEmpty(target))
            throw new DynamoDbException(
                "com.amazonaws.dynamodb.v20120810#MissingAuthenticationTokenException",
                "Missing Authentication Token");

        if (!target.StartsWith(TargetPrefix))
            throw new UnknownOperationException();

        await next(context);
    }
}
