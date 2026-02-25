using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MockDynamoDB.Tests.Samples.Backends;

public class MockDynamoDbBackend : IMockBackend
{
    private WebApplicationFactory<Program>? _factory;

    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public bool IsAvailable => true;

    public Task InitializeAsync()
    {
        AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true;

        _factory = new WebApplicationFactory<Program>();

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = _factory.Server.BaseAddress.ToString(),
            AuthenticationRegion = "us-east-1",
            HttpClientFactory = new InProcessHttpClientFactory(_factory.Server.CreateHandler())
        };

        Client = new AmazonDynamoDBClient(new BasicAWSCredentials("fake", "fake"), config);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_factory != null)
            await _factory.DisposeAsync();
    }

    private sealed class InProcessHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler);
    }
}
