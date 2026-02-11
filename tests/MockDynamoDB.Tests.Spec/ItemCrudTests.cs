using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class ItemCrudTests : IClassFixture<MockDynamoDbFixture>, IAsyncLifetime
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName = $"items-{Guid.NewGuid():N}";

    public ItemCrudTests(MockDynamoDbFixture fixture)
    {
        _client = fixture.Client;
    }

    public async ValueTask InitializeAsync()
    {
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
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
    }

    public async ValueTask DisposeAsync()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Fact]
    public async Task PutItem_AndGetItem_ReturnsStoredItem()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "profile" },
            ["name"] = new() { S = "Alice" }
        });

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "profile" }
        });

        Assert.True(response.IsItemSet);
        Assert.Equal("Alice", response.Item["name"].S);
    }

    [Fact]
    public async Task GetItem_NonExistent_ReturnsNoItem()
    {
        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "missing" },
            ["sk"] = new() { S = "missing" }
        });

        Assert.False(response.IsItemSet);
    }

    [Fact]
    public async Task PutItem_ReplacesExisting()
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "profile" }
        };

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["name"] = new() { S = "Alice" }
        });

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["name"] = new() { S = "Bob" }
        });

        var response = await _client.GetItemAsync(_tableName, key);
        Assert.Equal("Bob", response.Item["name"].S);
    }

    [Fact]
    public async Task DeleteItem_RemovesItem()
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "del1" },
            ["sk"] = new() { S = "item" }
        };

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["data"] = new() { S = "value" }
        });

        await _client.DeleteItemAsync(_tableName, key);

        var response = await _client.GetItemAsync(_tableName, key);
        Assert.False(response.IsItemSet);
    }

    [Fact]
    public async Task DeleteItem_WithReturnValuesAllOld_ReturnsDeletedItem()
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "del2" },
            ["sk"] = new() { S = "item" }
        };

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["data"] = new() { S = "value" }
        });

        var response = await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = key,
            ReturnValues = ReturnValue.ALL_OLD
        });

        Assert.Equal("value", response.Attributes["data"].S);
    }

    [Fact]
    public async Task PutItem_AllAttributeTypes()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "types" },
            ["sk"] = new() { S = "test" },
            ["stringAttr"] = new() { S = "hello" },
            ["numberAttr"] = new() { N = "42" },
            ["boolAttr"] = new() { BOOL = true },
            ["nullAttr"] = new() { NULL = true },
            ["listAttr"] = new() { L = [new() { S = "a" }, new() { N = "1" }] },
            ["mapAttr"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["nested"] = new() { S = "value" }
                }
            },
            ["stringSet"] = new() { SS = ["x", "y", "z"] },
            ["numberSet"] = new() { NS = ["1", "2", "3"] }
        };

        await _client.PutItemAsync(_tableName, item);

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "types" },
            ["sk"] = new() { S = "test" }
        });

        Assert.Equal("hello", response.Item["stringAttr"].S);
        Assert.Equal("42", response.Item["numberAttr"].N);
        Assert.True(response.Item["boolAttr"].BOOL);
        Assert.True(response.Item["nullAttr"].NULL);
        Assert.Equal(2, response.Item["listAttr"].L.Count);
        Assert.Equal("value", response.Item["mapAttr"].M["nested"].S);
        Assert.Equal(3, response.Item["stringSet"].SS.Count);
        Assert.Equal(3, response.Item["numberSet"].NS.Count);
    }

    [Fact]
    public async Task PutItem_TableNotExists_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _client.PutItemAsync("nonexistent", new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "test" }
            }));
    }
}
