using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Commands;

public sealed class TransactWriteItemsCommand(TransactionOperations ops) : DynamoDbCommand<TransactWriteItemsRequest, TransactWriteItemsResponse>
{
    public override string OperationName => "TransactWriteItems";
    protected override TransactWriteItemsResponse Execute(TransactWriteItemsRequest request) => ops.TransactWriteItems(request);
}

public sealed class TransactGetItemsCommand(TransactionOperations ops) : DynamoDbCommand<TransactGetItemsRequest, TransactGetItemsResponse>
{
    public override string OperationName => "TransactGetItems";
    protected override TransactGetItemsResponse Execute(TransactGetItemsRequest request) => ops.TransactGetItems(request);
}
