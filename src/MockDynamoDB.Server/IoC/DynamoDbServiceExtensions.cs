using MockDynamoDB.Core.Operations;
using MockDynamoDB.Core.Storage;
using MockDynamoDB.Server.Commands;
using MockDynamoDB.Server.Middleware;

namespace MockDynamoDB.Server.IoC;

public static class DynamoDbServiceExtensions
{
    public static IServiceCollection AddDynamoDbStores(this IServiceCollection services)
    {
        services.AddSingleton<ITableStore, InMemoryTableStore>();
        services.AddSingleton<IItemStore, InMemoryItemStore>();
        services.AddSingleton<ReaderWriterLockSlim>();
        return services;
    }

    public static IServiceCollection AddDynamoDbOperations(this IServiceCollection services)
    {
        services.AddSingleton<TableOperations>();
        services.AddSingleton<ItemOperations>();
        services.AddSingleton<QueryScanOperations>();
        services.AddSingleton<BatchOperations>();
        services.AddSingleton<TransactionOperations>();
        return services;
    }

    public static IServiceCollection AddDynamoDbCommands(this IServiceCollection services)
    {
        services.AddSingleton<IDynamoDbCommand, CreateTableCommand>();
        services.AddSingleton<IDynamoDbCommand, DeleteTableCommand>();
        services.AddSingleton<IDynamoDbCommand, DescribeTableCommand>();
        services.AddSingleton<IDynamoDbCommand, ListTablesCommand>();
        services.AddSingleton<IDynamoDbCommand, PutItemCommand>();
        services.AddSingleton<IDynamoDbCommand, GetItemCommand>();
        services.AddSingleton<IDynamoDbCommand, DeleteItemCommand>();
        services.AddSingleton<IDynamoDbCommand, UpdateItemCommand>();
        services.AddSingleton<IDynamoDbCommand, QueryCommand>();
        services.AddSingleton<IDynamoDbCommand, ScanCommand>();
        services.AddSingleton<IDynamoDbCommand, BatchGetItemCommand>();
        services.AddSingleton<IDynamoDbCommand, BatchWriteItemCommand>();
        services.AddSingleton<IDynamoDbCommand, TransactWriteItemsCommand>();
        services.AddSingleton<IDynamoDbCommand, TransactGetItemsCommand>();
        services.AddSingleton<DynamoDbRequestRouter>();
        return services;
    }
}
