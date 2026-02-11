using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class UpdateItemTests : IClassFixture<MockDynamoDbFixture>, IAsyncLifetime
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName = $"update-{Guid.NewGuid():N}";

    public UpdateItemTests(MockDynamoDbFixture fixture)
    {
        _client = fixture.Client;
    }

    public async ValueTask InitializeAsync()
    {
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });
    }

    public async ValueTask DisposeAsync()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Fact]
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
        Assert.Equal("Bob", result.Item["name"].S);
    }

    [Fact]
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
        Assert.Equal("8", result.Item["count"].N);
    }

    [Fact]
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
        Assert.False(result.Item.ContainsKey("temp"));
    }

    [Fact]
    public async Task UpdateItem_ConditionFails_ThrowsConditionalCheckFailedException()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "u4" },
            ["status"] = new() { S = "active" }
        });

        await Assert.ThrowsAsync<ConditionalCheckFailedException>(() =>
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
            }));
    }

    [Fact]
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

        Assert.Equal("2", result.Attributes["count"].N);
        Assert.Equal("u5", result.Attributes["pk"].S);
    }

    [Fact]
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
        Assert.True(result.IsItemSet);
        Assert.Equal("Created", result.Item["name"].S);
    }

    [Fact]
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
        Assert.Contains("a", result.Item["tags"].SS);
        Assert.Contains("b", result.Item["tags"].SS);
        Assert.Contains("c", result.Item["tags"].SS);
    }

    [Fact]
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
        Assert.DoesNotContain("b", result.Item["tags"].SS);
        Assert.Contains("a", result.Item["tags"].SS);
        Assert.Contains("c", result.Item["tags"].SS);
    }
}
