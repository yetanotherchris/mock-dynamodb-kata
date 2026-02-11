using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

public class BatchTransactionTests : IClassFixture<MockDynamoDbFixture>, IAsyncLifetime
{
    private readonly AmazonDynamoDBClient _client;
    private readonly string _tableName = $"batch-{Guid.NewGuid():N}";

    public BatchTransactionTests(MockDynamoDbFixture fixture)
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
    public async Task BatchWriteItem_PutMultiple()
    {
        var result = await _client.BatchWriteItemAsync(new BatchWriteItemRequest
        {
            RequestItems = new()
            {
                [_tableName] =
                [
                    new WriteRequest(new PutRequest(new() { ["pk"] = new() { S = "b1" }, ["data"] = new() { S = "v1" } })),
                    new WriteRequest(new PutRequest(new() { ["pk"] = new() { S = "b2" }, ["data"] = new() { S = "v2" } })),
                    new WriteRequest(new PutRequest(new() { ["pk"] = new() { S = "b3" }, ["data"] = new() { S = "v3" } }))
                ]
            }
        });

        Assert.Empty(result.UnprocessedItems);

        var scan = await _client.ScanAsync(new ScanRequest { TableName = _tableName });
        Assert.True(scan.Count >= 3);
    }

    [Fact]
    public async Task BatchGetItem_GetMultiple()
    {
        await _client.PutItemAsync(_tableName, new() { ["pk"] = new() { S = "bg1" }, ["val"] = new() { S = "x" } });
        await _client.PutItemAsync(_tableName, new() { ["pk"] = new() { S = "bg2" }, ["val"] = new() { S = "y" } });

        var result = await _client.BatchGetItemAsync(new BatchGetItemRequest
        {
            RequestItems = new()
            {
                [_tableName] = new KeysAndAttributes
                {
                    Keys =
                    [
                        new() { ["pk"] = new() { S = "bg1" } },
                        new() { ["pk"] = new() { S = "bg2" } },
                        new() { ["pk"] = new() { S = "bg-missing" } }
                    ]
                }
            }
        });

        Assert.Equal(2, result.Responses[_tableName].Count);
        Assert.Empty(result.UnprocessedKeys);
    }

    [Fact]
    public async Task TransactWriteItems_AllSucceed()
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
                        Item = new() { ["pk"] = new() { S = "tx1" }, ["data"] = new() { S = "a" } }
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = _tableName,
                        Item = new() { ["pk"] = new() { S = "tx2" }, ["data"] = new() { S = "b" } }
                    }
                }
            ]
        });

        var r1 = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "tx1" } });
        var r2 = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "tx2" } });
        Assert.True(r1.IsItemSet);
        Assert.True(r2.IsItemSet);
    }

    [Fact]
    public async Task TransactWriteItems_ConditionFails_NoneApplied()
    {
        await _client.PutItemAsync(_tableName, new() { ["pk"] = new() { S = "txf1" }, ["status"] = new() { S = "done" } });

        var ex = await Assert.ThrowsAsync<TransactionCanceledException>(() =>
            _client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _tableName,
                            Item = new() { ["pk"] = new() { S = "txf2" }, ["data"] = new() { S = "new" } }
                        }
                    },
                    new TransactWriteItem
                    {
                        ConditionCheck = new ConditionCheck
                        {
                            TableName = _tableName,
                            Key = new() { ["pk"] = new() { S = "txf1" } },
                            ConditionExpression = "#s = :expected",
                            ExpressionAttributeNames = new() { ["#s"] = "status" },
                            ExpressionAttributeValues = new() { [":expected"] = new() { S = "pending" } }
                        }
                    }
                ]
            }));

        Assert.NotNull(ex.CancellationReasons);

        // txf2 should NOT have been written
        var r = await _client.GetItemAsync(_tableName, new() { ["pk"] = new() { S = "txf2" } });
        Assert.False(r.IsItemSet);
    }

    [Fact]
    public async Task TransactGetItems_ReturnsItems()
    {
        await _client.PutItemAsync(_tableName, new() { ["pk"] = new() { S = "tg1" }, ["val"] = new() { S = "a" } });
        await _client.PutItemAsync(_tableName, new() { ["pk"] = new() { S = "tg2" }, ["val"] = new() { S = "b" } });

        var result = await _client.TransactGetItemsAsync(new TransactGetItemsRequest
        {
            TransactItems =
            [
                new TransactGetItem { Get = new Get { TableName = _tableName, Key = new() { ["pk"] = new() { S = "tg1" } } } },
                new TransactGetItem { Get = new Get { TableName = _tableName, Key = new() { ["pk"] = new() { S = "tg2" } } } },
                new TransactGetItem { Get = new Get { TableName = _tableName, Key = new() { ["pk"] = new() { S = "tg-missing" } } } }
            ]
        });

        Assert.Equal(3, result.Responses.Count);
        Assert.Equal("a", result.Responses[0].Item["val"].S);
        Assert.Equal("b", result.Responses[1].Item["val"].S);
        Assert.True(result.Responses[2].Item == null || result.Responses[2].Item.Count == 0);
    }
}
