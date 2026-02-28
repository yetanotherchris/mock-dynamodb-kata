using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Commands;

public sealed class CreateTableCommand(TableOperations ops) : DynamoDbCommand<CreateTableRequest, CreateTableResponse>
{
    public override string OperationName => "CreateTable";
    protected override CreateTableResponse Execute(CreateTableRequest request) => ops.CreateTable(request);
}

public sealed class DeleteTableCommand(TableOperations ops) : DynamoDbCommand<DeleteTableRequest, DeleteTableResponse>
{
    public override string OperationName => "DeleteTable";
    protected override DeleteTableResponse Execute(DeleteTableRequest request) => ops.DeleteTable(request);
}

public sealed class DescribeTableCommand(TableOperations ops) : DynamoDbCommand<DescribeTableRequest, DescribeTableResponse>
{
    public override string OperationName => "DescribeTable";
    protected override DescribeTableResponse Execute(DescribeTableRequest request) => ops.DescribeTable(request);
}

public sealed class ListTablesCommand(TableOperations ops) : DynamoDbCommand<ListTablesRequest, ListTablesResponse>
{
    public override string OperationName => "ListTables";
    protected override ListTablesResponse Execute(ListTablesRequest request) => ops.ListTables(request);
}
