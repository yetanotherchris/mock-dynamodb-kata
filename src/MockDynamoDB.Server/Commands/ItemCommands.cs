using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Commands;

public sealed class PutItemCommand(ItemOperations ops) : DynamoDbCommand<PutItemRequest, PutItemResponse>
{
    public override string OperationName => "PutItem";
    protected override PutItemResponse Execute(PutItemRequest request) => ops.PutItem(request);
}

public sealed class GetItemCommand(ItemOperations ops) : DynamoDbCommand<GetItemRequest, GetItemResponse>
{
    public override string OperationName => "GetItem";
    protected override GetItemResponse Execute(GetItemRequest request) => ops.GetItem(request);
}

public sealed class DeleteItemCommand(ItemOperations ops) : DynamoDbCommand<DeleteItemRequest, DeleteItemResponse>
{
    public override string OperationName => "DeleteItem";
    protected override DeleteItemResponse Execute(DeleteItemRequest request) => ops.DeleteItem(request);
}

public sealed class UpdateItemCommand(ItemOperations ops) : DynamoDbCommand<UpdateItemRequest, UpdateItemResponse>
{
    public override string OperationName => "UpdateItem";
    protected override UpdateItemResponse Execute(UpdateItemRequest request) => ops.UpdateItem(request);
}
