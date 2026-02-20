using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class QueryScanTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;
    private readonly string _tableName = $"qs-{Guid.NewGuid():N}";

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

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
    public async Task Query_PartitionKeyOnly()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });

        await Assert.That(result.Count).IsEqualTo(5);
    }

    [Test]
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

        await Assert.That(result.Count).IsEqualTo(5);
    }

    [Test]
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

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Query_ReverseOrder()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } },
            ScanIndexForward = false
        });

        await Assert.That(result.Items[0]["sk"].S).IsEqualTo("order#005");
        await Assert.That(result.Items[^1]["sk"].S).IsEqualTo("order#001");
    }

    [Test]
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

        await Assert.That(result.Count).IsEqualTo(2); // items with amount 40, 50
    }

    [Test]
    public async Task Query_WithLimit()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } },
            Limit = 2
        });

        await Assert.That(result.Items).HasCount().EqualTo(2);
        await Assert.That(result.LastEvaluatedKey).IsNotNull();
    }

    [Test]
    public async Task Scan_AllItems()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName
        });

        await Assert.That(result.Count).IsEqualTo(6); // 5 user1 + 1 user2
    }

    [Test]
    public async Task Scan_WithFilterExpression()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user2" } }
        });

        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Scan_WithLimit()
    {
        var result = await _client.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            Limit = 3
        });

        await Assert.That(result.Items).HasCount().EqualTo(3);
        await Assert.That(result.LastEvaluatedKey).IsNotNull();
    }

    [Test]
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

        await Assert.That(allItems).HasCount().EqualTo(6);
    }
}
