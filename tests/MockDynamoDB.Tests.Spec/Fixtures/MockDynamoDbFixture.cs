using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using TUnit.Core.Interfaces;

namespace MockDynamoDB.Tests.Spec.Fixtures;

public class MockDynamoDbFixture : IAsyncInitializer, IAsyncDisposable
{
    private WebApplicationFactory<Program>? _factory;

    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true;

        _factory = new WebApplicationFactory<Program>();

        var handler = _factory.Server.CreateHandler();
        AWSConfigs.HttpClientFactory = new TestHttpClientFactory(handler);

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = _factory.Server.BaseAddress.ToString(),
            AuthenticationRegion = "us-east-1"
        };

        var credentials = new BasicAWSCredentials("fake", "fake");
        Client = new AmazonDynamoDBClient(credentials, config);

        return Task.CompletedTask;
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
