using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Samples.Backends;

namespace MockDynamoDB.Tests.Samples;

public abstract class WorkingWithTablesTests(IMockBackend backend)
{
    private readonly AmazonDynamoDBClient _client = backend.Client;

    [Before(Test)]
    public Task SkipIfUnavailable()
    {
        if (!backend.IsAvailable)
            Skip.Test("Docker unavailable or moto container failed to start");
        return Task.CompletedTask;
    }

    [Test]
    public async Task CreateTable_Provisioned_ReturnsActiveStatus()
    {
        var tableName = $"MyTable-{Guid.NewGuid():N}";
        try
        {
            var response = await _client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                KeySchema =
                [
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE)
                ],
                AttributeDefinitions =
                [
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S)
                ],
                BillingMode = BillingMode.PROVISIONED,
                ProvisionedThroughput = new ProvisionedThroughput(5, 5)
            });

            await Assert.That(response.TableDescription.TableStatus).IsEqualTo(TableStatus.ACTIVE);
            await Assert.That(response.TableDescription.TableName).IsEqualTo(tableName);
        }
        finally
        {
            try { await _client.DeleteTableAsync(tableName); } catch { }
        }
    }

    [Test]
    public async Task CreateTable_DuplicateName_ThrowsResourceInUseException()
    {
        var tableName = $"MyTable-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("PK", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("PK", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        try
        {
            await Assert.That(() => _client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = [new KeySchemaElement("PK", KeyType.HASH)],
                AttributeDefinitions = [new AttributeDefinition("PK", ScalarAttributeType.S)],
                BillingMode = BillingMode.PAY_PER_REQUEST
            })).ThrowsExactly<ResourceInUseException>();
        }
        finally
        {
            try { await _client.DeleteTableAsync(tableName); } catch { }
        }
    }

    [Test]
    public async Task DeleteTable_RemovesTable()
    {
        var tableName = $"MyTable-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("PK", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("PK", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        await _client.DeleteTableAsync(tableName);

        await Assert.That(() => _client.DescribeTableAsync(tableName))
            .ThrowsExactly<ResourceNotFoundException>();
    }
}

[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class MockDynamoDB_WorkingWithTablesTests(MockDynamoDbBackend backend)
    : WorkingWithTablesTests(backend);

[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class Moto_WorkingWithTablesTests(MotoBackend backend)
    : WorkingWithTablesTests(backend);
