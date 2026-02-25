using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace MockDynamoDB.Tests.Samples.Backends;

/// <summary>
/// Connects to an already-running Moto server (default port 5000, override via
/// <c>MOTO_PORT</c> environment variable). Sets <see cref="IsAvailable"/> to false
/// and skips all Moto tests when the server is not reachable.
/// </summary>
public sealed class MotoBackend : IMockBackend
{
    public bool IsAvailable { get; private set; }
    public AmazonDynamoDBClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var port = Environment.GetEnvironmentVariable("MOTO_PORT") ?? "5000";
        var endpoint = $"http://127.0.0.1:{port}";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync(endpoint + "/");
            if (!response.IsSuccessStatusCode) return;
        }
        catch
        {
            return; // Moto not running — tests will skip
        }

        AWSConfigs.DisableDangerousDisablePathAndQueryCanonicalization = true;

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1"
        };
        Client = new AmazonDynamoDBClient(new BasicAWSCredentials("fake", "fake"), config);
        IsAvailable = true;
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
