using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Samples.Backends;

namespace MockDynamoDB.Tests.Samples;

public abstract class WorkingWithQueriesTests(IMockBackend backend)
{
    private readonly AmazonDynamoDBClient _client = backend.Client;
    private readonly string _tableName = $"MyTable-{Guid.NewGuid():N}";

    [Before(Test)]
    public async Task SetUp()
    {
        if (!backend.IsAvailable)
            Skip.Test("Moto server not running (start with: docker run -d -p 5000:5000 motoserver/moto:5.1.21)");

        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
            KeySchema =
            [
                new KeySchemaElement("PK", KeyType.HASH),
                new KeySchemaElement("SK", KeyType.RANGE)
            ],
            AttributeDefinitions =
            [
                new AttributeDefinition("PK", ScalarAttributeType.S),
                new AttributeDefinition("SK", ScalarAttributeType.S)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        // Seed data matching aws-samples query examples
        var items = new[]
        {
            ("Customer1", "order#001", "Alice", "100"),
            ("Customer1", "order#002", "Bob", "200"),
            ("Customer1", "order#003", "Alice", "150"),
            ("Customer2", "order#001", "Charlie", "300"),
            ("Customer2", "order#002", "Alice", "50")
        };

        foreach (var (pk, sk, customerName, amount) in items)
        {
            await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk },
                ["CustomerName"] = new() { S = customerName },
                ["Amount"] = new() { N = amount }
            });
        }
    }

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
    public async Task Query_WithFilterExpression_FiltersOnNonKeyAttribute()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            FilterExpression = "CustomerName = :cn",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "Customer1" },
                [":cn"] = new() { S = "Alice" }
            }
        });

        await Assert.That(result.Items).Count().IsEqualTo(2);
        await Assert.That(result.Items.All(i => i["CustomerName"].S == "Alice")).IsTrue();
    }

    [Test]
    public async Task Query_WithProjectionExpression_ReturnsOnlySpecifiedAttributes()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ProjectionExpression = "PK, SK, CustomerName",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "Customer1" }
            }
        });

        await Assert.That(result.Items).Count().IsEqualTo(3);
        await Assert.That(result.Items[0].ContainsKey("CustomerName")).IsTrue();
        await Assert.That(result.Items[0].ContainsKey("Amount")).IsFalse();
    }

    [Test]
    public async Task Query_WithSelectCount_ReturnsCountWithoutItems()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            Select = Select.COUNT,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "Customer1" }
            }
        });

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.Items == null || result.Items.Count == 0).IsTrue();
    }

    [Test]
    public async Task Query_WithConsistentRead_ReturnsCorrectResults()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ConsistentRead = true,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "Customer1" }
            }
        });

        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Query_WithReturnConsumedCapacityTotal_IncludesConsumedCapacityShape()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL,
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = "Customer1" }
            }
        });

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.ConsumedCapacity).IsNotNull();
        await Assert.That(result.ConsumedCapacity!.TableName).IsEqualTo(_tableName);
    }
}

[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class MockDynamoDB_WorkingWithQueriesTests(MockDynamoDbBackend backend)
    : WorkingWithQueriesTests(backend);

[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class Moto_WorkingWithQueriesTests(MotoBackend backend)
    : WorkingWithQueriesTests(backend);

