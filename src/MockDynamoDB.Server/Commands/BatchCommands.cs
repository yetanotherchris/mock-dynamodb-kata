using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Commands;

public sealed class BatchGetItemCommand(BatchOperations ops) : DynamoDbCommand<BatchGetItemRequest, BatchGetItemResponse>
{
    public override string OperationName => "BatchGetItem";
    protected override BatchGetItemResponse Execute(BatchGetItemRequest request) => ops.BatchGetItem(request);
}

public sealed class BatchWriteItemCommand(BatchOperations ops) : DynamoDbCommand<BatchWriteItemRequest, BatchWriteItemResponse>
{
    public override string OperationName => "BatchWriteItem";
    protected override BatchWriteItemResponse Execute(BatchWriteItemRequest request) => ops.BatchWriteItem(request);
}
