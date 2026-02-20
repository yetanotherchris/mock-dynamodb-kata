using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class LsiTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;
    private readonly string _tableName = $"lsi-{Guid.NewGuid():N}";

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

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
    public async Task Query_OnLsi_ReturnsSortedByLsiSortKey()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });

        await Assert.That(result.Count).IsEqualTo(3); // item#004 excluded (no lsiSk)
        await Assert.That(result.Items[0]["lsiSk"].S).IsEqualTo("2024-01-01");
        await Assert.That(result.Items[1]["lsiSk"].S).IsEqualTo("2024-03-10");
        await Assert.That(result.Items[2]["lsiSk"].S).IsEqualTo("2024-06-15");
    }

    [Test]
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

        await Assert.That(result.Count).IsEqualTo(2); // 2024-03-10 and 2024-06-15
    }

    [Test]
    public async Task Query_OnLsi_ItemsWithoutLsiSk_Excluded()
    {
        // Main table has 4 items
        var mainResult = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });
        await Assert.That(mainResult.Count).IsEqualTo(4);

        // LSI has 3 items (item#004 has no lsiSk)
        var lsiResult = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "lsi-index",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
        });
        await Assert.That(lsiResult.Count).IsEqualTo(3);
    }

    [Test]
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

        await Assert.That(result.Items[0]["lsiSk"].S).IsEqualTo("2024-06-15");
        await Assert.That(result.Items[^1]["lsiSk"].S).IsEqualTo("2024-01-01");
    }

    [Test]
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

        await Assert.That(result.Items).HasCount().EqualTo(1);
        var item = result.Items[0];
        await Assert.That(item["pk"].S).IsEqualTo("user1");
        await Assert.That(item["sk"].S).IsEqualTo("item#001");
        await Assert.That(item["lsiSk"].S).IsEqualTo("2024-01-01");
        await Assert.That(item["data"].S).IsEqualTo("first");
    }

    [Test]
    public async Task Query_OnNonExistentIndex_ThrowsValidationException()
    {
        AmazonDynamoDBException? ex = null;
        try
        {
            await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                IndexName = "nonexistent-index",
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new() { [":pk"] = new() { S = "user1" } }
            });
        }
        catch (AmazonDynamoDBException caught)
        {
            ex = caught;
        }

        await Assert.That(ex).IsNotNull();
        await Assert.That(ex!.Message).Contains("does not have the specified index");
    }
}
