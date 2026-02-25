using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Samples.Backends;

namespace MockDynamoDB.Tests.Samples;

public abstract class WorkingWithItemsTests(IMockBackend backend)
{
    private readonly AmazonDynamoDBClient _client = backend.Client;
    private readonly string _tableName = $"RetailDatabase-{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task SetUp()
    {
        if (!backend.IsAvailable)
            Skip.Test("Docker unavailable or moto container failed to start");

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
    public async Task PutItem_WithNestedMapAttribute_StoresItem()
    {
        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "jim.bob@somewhere.com" },
                ["sk"] = new() { S = "metadata" },
                ["name"] = new() { S = "Jim Bob" },
                ["address"] = new()
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["street"] = new() { S = "1 Somewhere Lane" },
                        ["city"] = new() { S = "Anytown" },
                        ["state"] = new() { S = "AW" },
                        ["zip"] = new() { S = "00000" }
                    }
                }
            }
        });

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "jim.bob@somewhere.com" },
            ["sk"] = new() { S = "metadata" }
        });

        await Assert.That(response.IsItemSet).IsTrue();
        await Assert.That(response.Item["name"].S).IsEqualTo("Jim Bob");
        await Assert.That(response.Item["address"].M["city"].S).IsEqualTo("Anytown");
    }

    [Test]
    public async Task GetItem_RetrievesStoredItem()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "jim.bob@somewhere.com" },
            ["sk"] = new() { S = "metadata" },
            ["name"] = new() { S = "Jim Bob" }
        });

        var response = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "jim.bob@somewhere.com" },
                ["sk"] = new() { S = "metadata" }
            }
        });

        await Assert.That(response.IsItemSet).IsTrue();
        await Assert.That(response.Item["pk"].S).IsEqualTo("jim.bob@somewhere.com");
    }

    [Test]
    public async Task UpdateItem_WithExpressionAttributeNames_UpdatesReservedWordField()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "jim.bob@somewhere.com" },
            ["sk"] = new() { S = "metadata" },
            ["name"] = new() { S = "Jim Bob" }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "jim.bob@somewhere.com" },
                ["sk"] = new() { S = "metadata" }
            },
            UpdateExpression = "SET #n = :newName",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#n"] = "name" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newName"] = new() { S = "Joe Bob" }
            }
        });

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "jim.bob@somewhere.com" },
            ["sk"] = new() { S = "metadata" }
        });

        await Assert.That(response.Item["name"].S).IsEqualTo("Joe Bob");
    }

    [Test]
    public async Task UpdateItemConditional_WhenConditionMet_UpdatesItem()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "profile" },
            ["status"] = new() { S = "active" }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "user1" },
                ["sk"] = new() { S = "profile" }
            },
            UpdateExpression = "SET #s = :newStatus",
            ConditionExpression = "#s = :expected",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#s"] = "status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newStatus"] = new() { S = "inactive" },
                [":expected"] = new() { S = "active" }
            }
        });

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "profile" }
        });

        await Assert.That(response.Item["status"].S).IsEqualTo("inactive");
    }

    [Test]
    public async Task UpdateItemConditional_WhenConditionFails_ThrowsException()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user2" },
            ["sk"] = new() { S = "profile" },
            ["status"] = new() { S = "active" }
        });

        await Assert.That(() => _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "user2" },
                ["sk"] = new() { S = "profile" }
            },
            UpdateExpression = "SET #s = :newStatus",
            ConditionExpression = "#s = :expected",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#s"] = "status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newStatus"] = new() { S = "inactive" },
                [":expected"] = new() { S = "pending" }
            }
        })).ThrowsExactly<ConditionalCheckFailedException>();
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

        await _client.DeleteItemAsync(new DeleteItemRequest { TableName = _tableName, Key = key });

        var response = await _client.GetItemAsync(_tableName, key);
        await Assert.That(response.IsItemSet).IsFalse();
    }

    [Test]
    public async Task DeleteItemConditional_WhenConditionMet_DeletesItem()
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "del2" },
            ["sk"] = new() { S = "item" }
        };

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["status"] = new() { S = "expired" }
        });

        await _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = key,
            ConditionExpression = "#s = :expected",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#s"] = "status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expected"] = new() { S = "expired" }
            }
        });

        var response = await _client.GetItemAsync(_tableName, key);
        await Assert.That(response.IsItemSet).IsFalse();
    }

    [Test]
    public async Task DeleteItemConditional_WhenConditionFails_ThrowsException()
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "del3" },
            ["sk"] = new() { S = "item" }
        };

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>(key)
        {
            ["status"] = new() { S = "active" }
        });

        await Assert.That(() => _client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = key,
            ConditionExpression = "#s = :expected",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#s"] = "status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expected"] = new() { S = "expired" }
            }
        })).ThrowsExactly<ConditionalCheckFailedException>();
    }

    [Test]
    public async Task PutItemConditional_AttributeNotExists_PreventsOverwrite()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "existing" },
            ["sk"] = new() { S = "item" },
            ["data"] = new() { S = "original" }
        });

        await Assert.That(() => _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "existing" },
                ["sk"] = new() { S = "item" },
                ["data"] = new() { S = "overwrite" }
            },
            ConditionExpression = "attribute_not_exists(pk)"
        })).ThrowsExactly<ConditionalCheckFailedException>();

        var response = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "existing" },
            ["sk"] = new() { S = "item" }
        });

        await Assert.That(response.Item["data"].S).IsEqualTo("original");
    }
}

[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class MockDynamoDB_WorkingWithItemsTests(MockDynamoDbBackend backend)
    : WorkingWithItemsTests(backend);

[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class Moto_WorkingWithItemsTests(MotoBackend backend)
    : WorkingWithItemsTests(backend);

