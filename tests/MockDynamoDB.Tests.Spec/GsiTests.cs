using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using MockDynamoDB.Tests.Spec.Fixtures;

namespace MockDynamoDB.Tests.Spec;

[ClassDataSource<MockDynamoDbFixture>(Shared = SharedType.PerTestSession)]
public class GsiTests(MockDynamoDbFixture fixture)
{
    private readonly AmazonDynamoDBClient _client = fixture.Client;
    private readonly string _tableName = $"gsi-{Guid.NewGuid():N}";

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
                new AttributeDefinition("gsiPk", ScalarAttributeType.S),
                new AttributeDefinition("gsiSk", ScalarAttributeType.S)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST,
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "gsi-by-type",
                    KeySchema =
                    [
                        new KeySchemaElement("gsiPk", KeyType.HASH),
                        new KeySchemaElement("gsiSk", KeyType.RANGE)
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ]
        });

        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#1" },
            ["sk"] = new() { S = "profile" },
            ["gsiPk"] = new() { S = "type#admin" },
            ["gsiSk"] = new() { S = "2024-01-01" },
            ["name"] = new() { S = "Alice" }
        });
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#2" },
            ["sk"] = new() { S = "profile" },
            ["gsiPk"] = new() { S = "type#admin" },
            ["gsiSk"] = new() { S = "2024-03-01" },
            ["name"] = new() { S = "Bob" }
        });
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#3" },
            ["sk"] = new() { S = "profile" },
            ["gsiPk"] = new() { S = "type#member" },
            ["gsiSk"] = new() { S = "2024-06-01" },
            ["name"] = new() { S = "Carol" }
        });
        // Item without gsiPk (not in GSI)
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#4" },
            ["sk"] = new() { S = "profile" },
            ["name"] = new() { S = "Dave" }
        });
    }

    [After(Test)]
    public async Task TearDown()
    {
        try { await _client.DeleteTableAsync(_tableName); } catch { }
    }

    [Test]
    public async Task CreateTable_WithGsi_DescribeTableReturnsGsi()
    {
        var result = await _client.DescribeTableAsync(_tableName);
        var gsis = result.Table.GlobalSecondaryIndexes;

        await Assert.That(gsis).IsNotNull();
        await Assert.That(gsis).Count().IsEqualTo(1);
        await Assert.That(gsis[0].IndexName).IsEqualTo("gsi-by-type");
        await Assert.That(gsis[0].IndexStatus).IsEqualTo(IndexStatus.ACTIVE);
    }

    [Test]
    public async Task Query_OnGsi_ReturnsSortedByGsiSortKey()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
        });

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.Items[0]["name"].S).IsEqualTo("Alice");
        await Assert.That(result.Items[1]["name"].S).IsEqualTo("Bob");
    }

    [Test]
    public async Task Query_OnGsi_WithSortKeyCondition()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk AND gsiSk > :d",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "type#admin" },
                [":d"] = new() { S = "2024-02-01" }
            }
        });

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Items[0]["name"].S).IsEqualTo("Bob");
    }

    [Test]
    public async Task Query_OnGsi_ItemsWithoutGsiKey_Excluded()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
        });

        // Dave has no gsiPk, so only Alice and Bob
        await Assert.That(result.Count).IsEqualTo(2);
        var names = result.Items.Select(i => i["name"].S).ToList();
        await Assert.That(names.Contains("Dave")).IsFalse();
    }

    [Test]
    public async Task Query_OnGsi_ReverseOrder()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } },
            ScanIndexForward = false
        });

        await Assert.That(result.Items[0]["name"].S).IsEqualTo("Bob");
        await Assert.That(result.Items[1]["name"].S).IsEqualTo("Alice");
    }

    [Test]
    public async Task Query_OnGsi_ReturnsAllAttributes()
    {
        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk AND gsiSk = :sk",
            ExpressionAttributeValues = new()
            {
                [":pk"] = new() { S = "type#admin" },
                [":sk"] = new() { S = "2024-01-01" }
            }
        });

        await Assert.That(result.Items).Count().IsEqualTo(1);
        var item = result.Items[0];
        await Assert.That(item["pk"].S).IsEqualTo("user#1");
        await Assert.That(item["sk"].S).IsEqualTo("profile");
        await Assert.That(item["gsiPk"].S).IsEqualTo("type#admin");
        await Assert.That(item["gsiSk"].S).IsEqualTo("2024-01-01");
        await Assert.That(item["name"].S).IsEqualTo("Alice");
    }

    [Test]
    public async Task Query_OnNonExistentGsi_ThrowsValidationException()
    {
        AmazonDynamoDBException? ex = null;
        try
        {
            await _client.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                IndexName = "nonexistent-gsi",
                KeyConditionExpression = "gsiPk = :pk",
                ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
            });
        }
        catch (AmazonDynamoDBException caught)
        {
            ex = caught;
        }

        await Assert.That(ex).IsNotNull();
        await Assert.That(ex!.Message).Contains("does not have the specified index");
    }

    [Test]
    public async Task Query_OnGsi_HashKeyOnlyNoRangeKey_ReturnsItems()
    {
        // A table with a GSI that has no range key
        var tableName2 = $"gsi-nsk-{Guid.NewGuid():N}";
        await _client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName2,
            KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
            AttributeDefinitions =
            [
                new AttributeDefinition("pk", ScalarAttributeType.S),
                new AttributeDefinition("category", ScalarAttributeType.S)
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST,
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "by-category",
                    KeySchema = [new KeySchemaElement("category", KeyType.HASH)],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ]
        });

        try
        {
            await _client.PutItemAsync(tableName2, new()
            {
                ["pk"] = new() { S = "item#1" },
                ["category"] = new() { S = "books" }
            });
            await _client.PutItemAsync(tableName2, new()
            {
                ["pk"] = new() { S = "item#2" },
                ["category"] = new() { S = "books" }
            });
            await _client.PutItemAsync(tableName2, new()
            {
                ["pk"] = new() { S = "item#3" },
                ["category"] = new() { S = "movies" }
            });

            var result = await _client.QueryAsync(new QueryRequest
            {
                TableName = tableName2,
                IndexName = "by-category",
                KeyConditionExpression = "category = :cat",
                ExpressionAttributeValues = new() { [":cat"] = new() { S = "books" } }
            });

            await Assert.That(result.Count).IsEqualTo(2);
        }
        finally
        {
            try { await _client.DeleteTableAsync(tableName2); } catch { }
        }
    }

    [Test]
    public async Task PutItem_UpdatesGsi_OldEntryReplaced()
    {
        // Put item initially under type#admin
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#5" },
            ["sk"] = new() { S = "profile" },
            ["gsiPk"] = new() { S = "type#admin" },
            ["gsiSk"] = new() { S = "2024-09-01" },
            ["name"] = new() { S = "Eve" }
        });

        // Verify in admin GSI partition
        var before = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
        });
        await Assert.That(before.Items.Any(i => i["name"].S == "Eve")).IsTrue();

        // Update item to change gsiPk
        await _client.PutItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#5" },
            ["sk"] = new() { S = "profile" },
            ["gsiPk"] = new() { S = "type#member" },
            ["gsiSk"] = new() { S = "2024-09-01" },
            ["name"] = new() { S = "Eve" }
        });

        // Verify removed from admin partition
        var adminAfter = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
        });
        await Assert.That(adminAfter.Items.Any(i => i["name"].S == "Eve")).IsFalse();

        // Verify now in member partition
        var memberAfter = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#member" } }
        });
        await Assert.That(memberAfter.Items.Any(i => i["name"].S == "Eve")).IsTrue();
    }

    [Test]
    public async Task DeleteItem_RemovesFromGsi()
    {
        await _client.DeleteItemAsync(_tableName, new()
        {
            ["pk"] = new() { S = "user#1" },
            ["sk"] = new() { S = "profile" }
        });

        var result = await _client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = "gsi-by-type",
            KeyConditionExpression = "gsiPk = :pk",
            ExpressionAttributeValues = new() { [":pk"] = new() { S = "type#admin" } }
        });

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Items[0]["name"].S).IsEqualTo("Bob");
    }
}
