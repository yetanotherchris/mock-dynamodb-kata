using System.Text.Json;

namespace MockDynamoDB.Server.Commands;

public interface IDynamoDbCommand
{
    string OperationName { get; }
    Task<byte[]> HandleAsync(Stream body, JsonSerializerOptions options);
}

public abstract class DynamoDbCommand<TRequest, TResponse> : IDynamoDbCommand
{
    public abstract string OperationName { get; }

    public async Task<byte[]> HandleAsync(Stream body, JsonSerializerOptions options)
    {
        var request = await JsonSerializer.DeserializeAsync<TRequest>(body, options);
        var response = Execute(request!);
        return JsonSerializer.SerializeToUtf8Bytes(response, options);
    }

    protected abstract TResponse Execute(TRequest request);
}
