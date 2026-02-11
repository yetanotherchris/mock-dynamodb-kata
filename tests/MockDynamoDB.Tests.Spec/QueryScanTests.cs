using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class QueryScanTests : IClassFixture<MockDynamoDbFixture>, IAsyncLifetime
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName = $"qs-{Guid.NewGuid():N}";

    public QueryScanTests(MockDynamoDbFixture fixture)
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

        // Seed data
        for (int i = 1; i <= 5; i++)
        {
            await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = "user1" },
                ["sk"] = new() { S = $"order#{i:D3}" },
                ["amount"] = new() { N = (i * 10).ToString() }
            });
        }

        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "user2" },
            ["sk"] = new() { S = "order#001" },
            ["amount"] = new() { N = "99" }
        });
    }

    public async ValueTask DisposeAsync()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Fact]
    public async Task Query_PartitionKeyOnly()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task Query_WithSortKeyBeginsWith()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "user1" },
                [":prefix"] = new() { S = "order#00" }
            }
        });

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task Query_WithSortKeyBetween()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk AND sk BETWEEN :low AND :high",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "user1" },
                [":low"] = new() { S = "order#002" },
                [":high"] = new() { S = "order#004" }
            }
        });

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Query_ReverseOrder()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } },
            ScanIndexForward = false
        });

        Assert.Equal("order#005", result.Items[0]["sk"].S);
        Assert.Equal("order#001", result.Items[^1]["sk"].S);
    }

    [Fact]
    public async Task Query_WithFilterExpression()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            FilterExpression = "amount > :minAmount",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "user1" },
                [":minAmount"] = new() { N = "30" }
            }
        });

        Assert.Equal(2, result.Count); // items with amount 40, 50
    }

    [Fact]
    public async Task Query_WithLimit()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } },
            Limit = 2
        });

        Assert.Equal(2, result.Items.Count);
        Assert.NotNull(result.LastEvaluatedKey);
    }

    [Fact]
    public async Task Scan_AllItems()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName
        });

        Assert.Equal(6, result.Count); // 5 user1 + 1 user2
    }

    [Fact]
    public async Task Scan_WithFilterExpression()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user2" } }
        });

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task Scan_WithLimit()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            Limit = 3
        });

        Assert.Equal(3, result.Items.Count);
        Assert.NotNull(result.LastEvaluatedKey);
    }

    [Fact]
    public async Task Scan_Pagination()
    {
        var allItems = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            var result = await _client.ScanAsync(new ScanRequest
            {
                TableName = _tableName,
                Limit = 2,
                ExclusiveStartKey = lastKey
            });

            allItems.AddRange(result.Items);
            lastKey = result.LastEvaluatedKey;
        } while (lastKey != null && lastKey.Count > 0);

        Assert.Equal(6, allItems.Count);
    }
}
