using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class TableOperationTests : IClassFixture<MockDynamoDbFixture>
{
    private readonly AmazonDynamoDBClient _client;

    public TableOperationTests(MockDynamoDbFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
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

        Assert.Equal("ACTIVE", response.TableDescription.TableStatus.Value);
        Assert.Equal(tableName, response.TableDescription.TableName);

        await _client.DeleteTableAsync(tableName);
    }

    [Fact]
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

        Assert.Equal("ACTIVE", response.TableDescription.TableStatus.Value);
        Assert.Equal(2, response.TableDescription.KeySchema.Count);

        await _client.DeleteTableAsync(tableName);
    }

    [Fact]
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

        await Assert.ThrowsAsync<ResourceInUseException>(() =>
            _client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
                AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
                BillingMode = BillingMode.PAY_PER_REQUEST
            }));

        await _client.DeleteTableAsync(tableName);
    }

    [Fact]
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

        Assert.Equal(tableName, response.Table.TableName);
        Assert.Equal("ACTIVE", response.Table.TableStatus.Value);

        await _client.DeleteTableAsync(tableName);
    }

    [Fact]
    public async Task DescribeTable_NotExists_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _client.DescribeTableAsync("nonexistent-table"));
    }

    [Fact]
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

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _client.DescribeTableAsync(tableName));
    }

    [Fact]
    public async Task DeleteTable_NotExists_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _client.DeleteTableAsync("nonexistent-table"));
    }

    [Fact]
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

        Assert.Contains(name1, response.TableNames);
        Assert.Contains(name2, response.TableNames);

        await _client.DeleteTableAsync(name1);
        await _client.DeleteTableAsync(name2);
    }
}
