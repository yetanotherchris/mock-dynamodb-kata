using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MockDynamoDB.Tests.Spec.Fixtures;

public class MockDynamoDbFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _httpClient;

    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _httpClient = _factory.CreateClient();

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = _httpClient.BaseAddress!.ToString(),
            AuthenticationRegion = "us-east-1"
        };

        var credentials = new BasicAWSCredentials("fake", "fake");

        config.HttpClientFactory = new TestHttpClientFactory(_factory.Server.CreateHandler());
        Client = new AmazonDynamoDBClient(credentials, config);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        _httpClient?.Dispose();
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
