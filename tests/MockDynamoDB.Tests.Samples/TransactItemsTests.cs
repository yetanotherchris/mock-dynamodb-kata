using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Samples.Backends;

namespace MockDynamoDB.Tests.Samples;

public abstract class TransactItemsTests(IMockBackend backend)
{
    private readonly AmazonDynamoDBClient _client = backend.Client;
    private readonly string _tableName = $"TransactItems-{Guid.NewGuid():N}";

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
    public async Task TransactWriteItems_WritesMultipleItemsAtomically()
    {
        await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new() { S = "customer1" },
                            ["sk"] = new() { S = "order#001" },
                            ["amount"] = new() { N = "100" }
                        }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new() { S = "customer1" },
                            ["sk"] = new() { S = "order#002" },
                            ["amount"] = new() { N = "200" }
                        }
                    }
                }
            ]
        });

        var r1 = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer1" },
            ["sk"] = new() { S = "order#001" }
        });
        var r2 = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer1" },
            ["sk"] = new() { S = "order#002" }
        });

        await Assert.That(r1.IsItemSet).IsTrue();
        await Assert.That(r2.IsItemSet).IsTrue();
        await Assert.That(r1.Item["amount"].N).IsEqualTo("100");
        await Assert.That(r2.Item["amount"].N).IsEqualTo("200");
    }

    [Test]
    public async Task TransactWriteItems_WhenConditionFails_RollsBackAll()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "inventory" },
            ["sk"] = new() { S = "itemA" },
            ["stock"] = new() { N = "0" }
        });

        TransactionCanceledException? ex = null;
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new() { S = "order" },
                                ["sk"] = new() { S = "new" },
                                ["status"] = new() { S = "placed" }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        ConditionCheck = new ConditionCheck
                        {
                            TableName = _tableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new() { S = "inventory" },
                                ["sk"] = new() { S = "itemA" }
                            },
                            ConditionExpression = "stock > :zero",
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":zero"] = new() { N = "0" }
                            }
                        }
                    }
                ]
            });
        }
        catch (TransactionCanceledException caught)
        {
            ex = caught;
        }

        await Assert.That(ex).IsNotNull();

        var order = await _client.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "order" },
            ["sk"] = new() { S = "new" }
        });
        await Assert.That(order.IsItemSet).IsFalse();
    }

    [Test]
    public async Task TransactGetItems_RetrievesMultipleItems()
    {
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer1" },
            ["sk"] = new() { S = "order#001" },
            ["amount"] = new() { N = "100" }
        });
        await _client.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "customer2" },
            ["sk"] = new() { S = "order#001" },
            ["amount"] = new() { N = "200" }
        });

        var result = await _client.TransactGetItemsAsync(new TransactGetItemsRequest
        {
            TransactItems =
            [
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new() { S = "customer1" },
                            ["sk"] = new() { S = "order#001" }
                        }
                    }
                },
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new() { S = "customer2" },
                            ["sk"] = new() { S = "order#001" }
                        }
                    }
                },
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new() { S = "missing" },
                            ["sk"] = new() { S = "order#001" }
                        }
                    }
                }
            ]
        });

        await Assert.That(result.Responses).Count().IsEqualTo(3);
        await Assert.That(result.Responses[0].Item["amount"].N).IsEqualTo("100");
        await Assert.That(result.Responses[1].Item["amount"].N).IsEqualTo("200");
        await Assert.That(result.Responses[2].Item == null || result.Responses[2].Item.Count == 0).IsTrue();
    }
}

[ClassDataSource<MockDynamoDbBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class MockDynamoDB_TransactItemsTests(MockDynamoDbBackend backend)
    : TransactItemsTests(backend);

[ClassDataSource<MotoBackend>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class Moto_TransactItemsTests(MotoBackend backend)
    : TransactItemsTests(backend);

