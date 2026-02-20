using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class UpdateItemTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;
    private readonly string _tableName = $"update-{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task SetUp()
    {
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });
    }

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
    public async Task UpdateItem_SetAttribute()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u1" },
            ["name"] = new() { S = "Alice" }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u1" } },
            UpdateExpression = "SET #n = :val",
            ExpressionAttributeNames = new() { ["#n"] = "name" },
            ExpressionAttributeValues = new() { [":val"] = new() { S = "Bob" } }
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "u1" } });
        await Assert.That(result.Item["name"].S).IsEqualTo("Bob");
    }

    [Test]
    public async Task UpdateItem_Arithmetic()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u2" },
            ["count"] = new() { N = "5" }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u2" } },
            UpdateExpression = "SET #c = #c + :inc",
            ExpressionAttributeNames = new() { ["#c"] = "count" },
            ExpressionAttributeValues = new() { [":inc"] = new() { N = "3" } }
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "u2" } });
        await Assert.That(result.Item["count"].N).IsEqualTo("8");
    }

    [Test]
    public async Task UpdateItem_Remove()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u3" },
            ["temp"] = new() { S = "remove-me" }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u3" } },
            UpdateExpression = "REMOVE temp"
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "u3" } });
        await Assert.That(result.Item.ContainsKey("temp")).IsFalse();
    }

    [Test]
    public async Task UpdateItem_ConditionFails_ThrowsConditionalCheckFailedException()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u4" },
            ["status"] = new() { S = "active" }
        });

        await Assert.That(() =>
            _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new() { ["pk"] = new() { S = "u4" } },
                UpdateExpression = "SET #s = :new",
                ConditionExpression = "#s = :expected",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new()
                {
                    [":new"] = new() { S = "inactive" },
                    [":expected"] = new() { S = "wrong" }
                }
            })).ThrowsExactly<ConditionalCheckFailedException>();
    }

    [Test]
    public async Task UpdateItem_ReturnAllNew()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u5" },
            ["count"] = new() { N = "1" }
        });

        var result = await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u5" } },
            UpdateExpression = "SET #c = #c + :inc",
            ExpressionAttributeNames = new() { ["#c"] = "count" },
            ExpressionAttributeValues = new() { [":inc"] = new() { N = "1" } },
            ReturnValues = ReturnValue.ALL_NEW
        });

        await Assert.That(result.Attributes["count"].N).IsEqualTo("2");
        await Assert.That(result.Attributes["pk"].S).IsEqualTo("u5");
    }

    [Test]
    public async Task UpdateItem_Upsert_CreatesNewItem()
    {
        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "new-item" } },
            UpdateExpression = "SET #n = :val",
            ExpressionAttributeNames = new() { ["#n"] = "name" },
            ExpressionAttributeValues = new() { [":val"] = new() { S = "Created" } }
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "new-item" } });
        await Assert.That(result.IsItemSet).IsTrue();
        await Assert.That(result.Item["name"].S).IsEqualTo("Created");
    }

    [Test]
    public async Task UpdateItem_AddToSet()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u6" },
            ["tags"] = new() { SS = ["a", "b"] }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u6" } },
            UpdateExpression = "ADD tags :newTags",
            ExpressionAttributeValues = new() { [":newTags"] = new() { SS = ["c"] } }
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "u6" } });
        await Assert.That(result.Item["tags"].SS).Contains("a");
        await Assert.That(result.Item["tags"].SS).Contains("b");
        await Assert.That(result.Item["tags"].SS).Contains("c");
    }

    [Test]
    public async Task UpdateItem_DeleteFromSet()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u7" },
            ["tags"] = new() { SS = ["a", "b", "c"] }
        });

        await _client.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new() { ["pk"] = new() { S = "u7" } },
            UpdateExpression = "DELETE tags :removeTags",
            ExpressionAttributeValues = new() { [":removeTags"] = new() { SS = ["b"] } }
        });

        var result = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "u7" } });
        await Assert.That(result.Item["tags"].SS).DoesNotContain("b");
        await Assert.That(result.Item["tags"].SS).Contains("a");
        await Assert.That(result.Item["tags"].SS).Contains("c");
    }
}
