using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class LsiTests : IClassFixture<MockDynamoDbFixture>, IAsyncLifetime
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName = $"lsi-{Guid.NewGuid():N}";

    public LsiTests(MockDynamoDbFixture fixture)
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
                new AttributeDefinition("sk", ScalarAttributeType.S),
                new AttributeDefinition("lsiSk", ScalarAttributeType.S)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST,
            LocalSecondaryIndexes =
            [
                new LocalSecondaryIndex
                {
                    IndexName = "lsi-index",
                    KeySchema =
                    [
                        new KeySchemaElement("pk", KeyType.HASH),
                        new KeySchemaElement("lsiSk", KeyType.RANGE)
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ]
        });

        // Items with lsiSk (indexed)
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "item#001" },
            ["lsiSk"] = new() { S = "2024-01-01" },
            ["data"] = new() { S = "first" }
        });
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "item#002" },
            ["lsiSk"] = new() { S = "2024-06-15" },
            ["data"] = new() { S = "second" }
        });
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "item#003" },
            ["lsiSk"] = new() { S = "2024-03-10" },
            ["data"] = new() { S = "third" }
        });
        // Item WITHOUT lsiSk (not indexed)
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user1" },
            ["sk"] = new() { S = "item#004" },
            ["data"] = new() { S = "no-lsi-sk" }
        });
    }

    public async ValueTask DisposeAsync()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Fact]
    public async Task Query_OnLsi_ReturnsSortedByLsiSortKey()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });

        Assert.Equal(3, result.Count); // item#004 excluded (no lsiSk)
        Assert.Equal("2024-01-01", result.Items[0]["lsiSk"].S);
        Assert.Equal("2024-03-10", result.Items[1]["lsiSk"].S);
        Assert.Equal("2024-06-15", result.Items[2]["lsiSk"].S);
    }

    [Fact]
    public async Task Query_OnLsi_WithSortKeyCondition()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk AND lsiSk > :d",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "user1" },
                [":d"] = new() { S = "2024-02-01" }
            }
        });

        Assert.Equal(2, result.Count); // 2024-03-10 and 2024-06-15
    }

    [Fact]
    public async Task Query_OnLsi_ItemsWithoutLsiSk_Excluded()
    {
        // Main table has 4 items
        var mainResult = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });
        Assert.Equal(4, mainResult.Count);

        // LSI has 3 items (item#004 has no lsiSk)
        var lsiResult = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });
        Assert.Equal(3, lsiResult.Count);
    }

    [Fact]
    public async Task Query_OnLsi_ReverseOrder()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } },
            ScanIndexForward = false
        });

        Assert.Equal("2024-06-15", result.Items[0]["lsiSk"].S);
        Assert.Equal("2024-01-01", result.Items[^1]["lsiSk"].S);
    }

    [Fact]
    public async Task Query_OnLsi_ReturnsAllAttributes()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk AND lsiSk = :sk",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "user1" },
                [":sk"] = new() { S = "2024-01-01" }
            }
        });

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("user1", item["pk"].S);
        Assert.Equal("item#001", item["sk"].S);
        Assert.Equal("2024-01-01", item["lsiSk"].S);
        Assert.Equal("first", item["data"].S);
    }

    [Fact]
    public async Task Query_OnNonExistentIndex_ThrowsValidationException()
    {
        var ex = await Assert.ThrowsAsync<AmazonDynamoDBException>(() =>
            _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                IndexName = "nonexistent-index",
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
            }));

        Assert.Contains("does not have the specified index", ex.Message);
    }
}
