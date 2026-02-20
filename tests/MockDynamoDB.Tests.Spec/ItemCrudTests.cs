using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class ItemCrudTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;
    private readonly string _tableName = $"items-{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task SetUp()
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

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
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

        await Assert.That(response.IsItemSet).IsTrue();
        await Assert.That(response.Item["name"].S).IsEqualTo("Alice");
    }

    [Test]
    public async Task GetItem_NonExistent_ReturnsNoItem()
    {
        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "missing" },
            ["sk"] = new() { S = "missing" }
        });

        await Assert.That(response.IsItemSet).IsFalse();
    }

    [Test]
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
        await Assert.That(response.Item["name"].S).IsEqualTo("Bob");
    }

    [Test]
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
        await Assert.That(response.IsItemSet).IsFalse();
    }

    [Test]
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

        await Assert.That(response.Attributes["data"].S).IsEqualTo("value");
    }

    [Test]
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

        await Assert.That(response.Item["stringAttr"].S).IsEqualTo("hello");
        await Assert.That(response.Item["numberAttr"].N).IsEqualTo("42");
        await Assert.That(response.Item["boolAttr"].BOOL).IsTrue();
        await Assert.That(response.Item["nullAttr"].NULL).IsTrue();
        await Assert.That(response.Item["listAttr"].L).Count().IsEqualTo(2);
        await Assert.That(response.Item["mapAttr"].M["nested"].S).IsEqualTo("value");
        await Assert.That(response.Item["stringSet"].SS).Count().IsEqualTo(3);
        await Assert.That(response.Item["numberSet"].NS).Count().IsEqualTo(3);
    }

    [Test]
    public async Task PutItem_TableNotExists_ThrowsResourceNotFoundException()
    {
        await Assert.That(() =>
            _client.PutItemAsync("nonexistent", new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "test" }
            })).ThrowsExactly<ResourceNotFoundException>();
    }
}
