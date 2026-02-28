using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MockDynamoDB.Core.Operations;
using MockDynamoDB.Core.Storage;
using MockDynamoDB.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddSingleton<ITableStore, InMemoryTableStore>();
builder.Services.AddSingleton<IItemStore, InMemoryItemStore>();
builder.Services.AddSingleton<ReaderWriterLockSlim>();
builder.Services.AddSingleton<TableOperations>();
builder.Services.AddSingleton<ItemOperations>();
builder.Services.AddSingleton<QueryScanOperations>();
builder.Services.AddSingleton<BatchOperations>();
builder.Services.AddSingleton<TransactionOperations>();
builder.Services.AddSingleton<DynamoDbRequestRouter>();

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
        await context.Response.WriteAsync("""{"status":"ok","service":"mock-dynamodb"}""");
    }
};
app.MapHealthChecks("/", healthCheckOptions);
app.MapHealthChecks("/healthz", healthCheckOptions);

var router = app.Services.GetRequiredService<DynamoDbRequestRouter>();
app.MapPost("/", async context => await router.HandleRequest(context));

app.Run();

public partial class Program { }
