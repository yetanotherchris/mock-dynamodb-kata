using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MockDynamoDB.Tests.Spec.Fixtures;

public class MockDynamoDbFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true;

        _factory = new WebApplicationFactory<Program>();

        var handler = _factory.Server.CreateHandler();
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = _factory.Server.BaseAddress.ToString(),
            AuthenticationRegion = "us-east-1"
        };

        config.HttpClientFactory = new TestHttpClientFactory(handler);

        var credentials = new BasicAWSCredentials("fake", "fake");
        Client = new AmazonDynamoDBClient(credentials, config);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_factory != null)
            await _factory.DisposeAsync();
    }

    private class TestHttpClientFactory : Amazon.Runtime.HttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            return new HttpClient(_handler);
        }
    }
}
