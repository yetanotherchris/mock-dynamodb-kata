using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Operations;

namespace MockDynamoDB.Server.Commands;

public sealed class QueryCommand(QueryScanOperations ops) : DynamoDbCommand<QueryRequest, QueryResponse>
{
    public override string OperationName => "Query";
    protected override QueryResponse Execute(QueryRequest request) => ops.Query(request);
}

public sealed class ScanCommand(QueryScanOperations ops) : DynamoDbCommand<ScanRequest, ScanResponse>
{
    public override string OperationName => "Scan";
    protected override ScanResponse Execute(ScanRequest request) => ops.Scan(request);
}
