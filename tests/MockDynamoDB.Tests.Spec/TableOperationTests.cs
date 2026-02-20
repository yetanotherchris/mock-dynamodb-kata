using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class TableOperationTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;

    [Test]
    public async Task CreateTable_SimpleHashKey_ReturnsActive()
    {
        var tableName = $"test-{Guid.NewGuid():N}";
        var response = await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        await Assert.That(response.TableDescription.TableStatus.Value).IsEqualTo("ACTIVE");
        await Assert.That(response.TableDescription.TableName).IsEqualTo(tableName);

        await _client.DeleteTableAsync(tableName);
    }

    [Test]
    public async Task CreateTable_HashAndRangeKey_ReturnsActive()
    {
        var tableName = $"test-{Guid.NewGuid():N}";
        var response = await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema =
            [
                new KeySchemaElement("pk", KeyType.HASH),
                new KeySchemaElement("sk", KeyType.RANGE)
            ],
            AttributeDefinitions =
            [
                new AttributeDefinition("pk", ScalarAttributeType.S),
                new AttributeDefinition("sk", ScalarAttributeType.S)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        await Assert.That(response.TableDescription.TableStatus.Value).IsEqualTo("ACTIVE");
        await Assert.That(response.TableDescription.KeySchema).HasCount().EqualTo(2);

        await _client.DeleteTableAsync(tableName);
    }

    [Test]
    public async Task CreateTable_AlreadyExists_ThrowsResourceInUseException()
    {
        var tableName = $"test-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        await Assert.That(() =>
            _client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
                AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
                BillingMode = BillingMode.PAY_PER_REQUEST
            })).ThrowsExactly<ResourceInUseException>();

        await _client.DeleteTableAsync(tableName);
    }

    [Test]
    public async Task DescribeTable_Exists_ReturnsTableInfo()
    {
        var tableName = $"test-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        var response = await _client.DescribeTableAsync(tableName);

        await Assert.That(response.Table.TableName).IsEqualTo(tableName);
        await Assert.That(response.Table.TableStatus.Value).IsEqualTo("ACTIVE");

        await _client.DeleteTableAsync(tableName);
    }

    [Test]
    public async Task DescribeTable_NotExists_ThrowsResourceNotFoundException()
    {
        await Assert.That(() =>
            _client.DescribeTableAsync("nonexistent-table"))
            .ThrowsExactly<ResourceNotFoundException>();
    }

    [Test]
    public async Task DeleteTable_Exists_RemovesTable()
    {
        var tableName = $"test-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        await _client.DeleteTableAsync(tableName);

        await Assert.That(() =>
            _client.DescribeTableAsync(tableName))
            .ThrowsExactly<ResourceNotFoundException>();
    }

    [Test]
    public async Task DeleteTable_NotExists_ThrowsResourceNotFoundException()
    {
        await Assert.That(() =>
            _client.DeleteTableAsync("nonexistent-table"))
            .ThrowsExactly<ResourceNotFoundException>();
    }

    [Test]
    public async Task ListTables_ReturnsAllTableNames()
    {
        var name1 = $"test-a-{Guid.NewGuid():N}";
        var name2 = $"test-b-{Guid.NewGuid():N}";

        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = name1,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = name2,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        var response = await _client.ListTablesAsync();

        await Assert.That(response.TableNames).Contains(name1);
        await Assert.That(response.TableNames).Contains(name2);

        await _client.DeleteTableAsync(name1);
        await _client.DeleteTableAsync(name2);
    }
}
