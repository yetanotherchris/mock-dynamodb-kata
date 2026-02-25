using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Samples.Backends;

namespace MockDynamoDB.Tests.Samples;

public abstract class BatchItemsTests(IMockBackend backend)
{
    private readonly AmazonDynamoDBClient _client = backend.Client;
    private readonly string _tableName = $"BatchItems-{Guid.NewGuid():N}";

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
    public async Task BatchWriteItem_PutsMultipleItems()
    {
        var result = await _client.BatchWriteItemAsync(new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [_tableName] =
                [
                    new WriteRequest(new PutRequest(new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new() { S = "customer1" }, ["sk"] = new() { S = "order#001" },
                        ["amount"] = new() { N = "100" }
                    })),
                    new WriteRequest(new PutRequest(new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new() { S = "customer1" }, ["sk"] = new() { S = "order#002" },
                        ["amount"] = new() { N = "200" }
                    })),
                    new WriteRequest(new PutRequest(new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new() { S = "customer2" }, ["sk"] = new() { S = "order#001" },
                        ["amount"] = new() { N = "150" }
                    }))
                ]
            }
        });

        await Assert.That(result.UnprocessedItems).IsEmpty();

        var scan = await _client.ScanAsync(new ScanRequest { TableName = _tableName });
        await Assert.That(scan.Items).Count().IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task BatchGetItem_RetrievesMultipleItems()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer1" }, ["sk"] = new() { S = "order#001" },
            ["amount"] = new() { N = "100" }
        });
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer2" }, ["sk"] = new() { S = "order#001" },
            ["amount"] = new() { N = "200" }
        });

        var result = await _client.BatchGetItemAsync(new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes
                {
                    Keys =
                    [
                        new Dictionary<string, AttributeValue>
                            { ["pk"] = new() { S = "customer1" }, ["sk"] = new() { S = "order#001" } },
                        new Dictionary<string, AttributeValue>
                            { ["pk"] = new() { S = "customer2" }, ["sk"] = new() { S = "order#001" } }
                    ]
                }
            }
        });

        await Assert.That(result.Responses[_tableName]).Count().IsEqualTo(2);
        await Assert.That(result.UnprocessedKeys).IsEmpty();
    }

    [Test]
    public async Task BatchGetItem_MissingKeysAbsentFromResponse()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "exists" }, ["sk"] = new() { S = "item" },
            ["data"] = new() { S = "value" }
        });

        var result = await _client.BatchGetItemAsync(new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes
                {
                    Keys =
                    [
                        new Dictionary<string, AttributeValue>
                            { ["pk"] = new() { S = "exists" }, ["sk"] = new() { S = "item" } },
                        new Dictionary<string, AttributeValue>
                            { ["pk"] = new() { S = "missing" }, ["sk"] = new() { S = "item" } }
                    ]
                }
            }
        });

        await Assert.That(result.Responses[_tableName]).Count().IsEqualTo(1);
        await Assert.That(result.Responses[_tableName][0]["pk"].S).IsEqualTo("exists");
    }
}

[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class MockDynamoDB_BatchItemsTests(MockDynamoDbBackend backend)
    : BatchItemsTests(backend);

[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class Moto_BatchItemsTests(MotoBackend backend)
    : BatchItemsTests(backend);
