using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MockDynamoDB.Server.IoC;
using MockDynamoDB.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services
    .AddDynamoDbStores()
    .AddDynamoDbOperations()
    .AddDynamoDbCommands();

var portStr = Environment.GetEnvironmentVariable("MOCK_DYNAMODB_PORT");
var port = 8000;
if (portStr != null && int.TryParse(portStr, out var envPort))
    port = envPort;

foreach (var arg in args)
{
    if (arg.StartsWith("--port=") && int.TryParse(arg["--port=".Length..], out var argPort))
        port = argPort;
}

builder.WebHost.UseUrls($"http://*:{port}");

var app = builder.Build();

var healthCheckOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = "ok", service = "mock-dynamodb" }));
    }
};
app.MapHealthChecks("/", healthCheckOptions);
app.MapHealthChecks("/healthz", healthCheckOptions);

app.UseMiddleware<DynamoDbErrorMiddleware>();
app.UseMiddleware<DynamoDbValidationMiddleware>();

var router = app.Services.GetRequiredService<DynamoDbRequestRouter>();
app.MapPost("/", async context => await router.HandleRequest(context));

app.Run();

public partial class Program { }
