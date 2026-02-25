using Amazon.DynamoDBv2;
using TUnit.Core.Interfaces;

namespace MockDynamoDB.Tests.Samples.Backends;

public interface IMockBackend : IAsyncInitializer, IAsyncDisposable
{
    AmazonDynamoDBClient Client { get; }
    bool IsAvailable { get; }
}
