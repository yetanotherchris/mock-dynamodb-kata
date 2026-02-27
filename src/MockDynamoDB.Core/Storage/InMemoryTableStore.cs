using System.Collections.Concurrent;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Storage;

public sealed class InMemoryTableStore : ITableStore
{
    private readonly ConcurrentDictionary<string, TableDefinition> _tables = new();

    public void CreateTable(TableDefinition table)
    {
        if (!_tables.TryAdd(table.TableName, table))
            throw new ResourceInUseException($"Table already exists: {table.TableName}");
    }

    public TableDefinition GetTable(string tableName)
    {
        if (!_tables.TryGetValue(tableName, out var table))
            throw new ResourceNotFoundException($"Requested resource not found: Table: {tableName} not found");
        return table;
    }

    public TableDefinition DeleteTable(string tableName)
    {
        if (!_tables.TryRemove(tableName, out var table))
            throw new ResourceNotFoundException($"Requested resource not found: Table: {tableName} not found");
        return table;
    }

    public bool TableExists(string tableName) => _tables.ContainsKey(tableName);

    public List<string> ListTableNames() => _tables.Keys.OrderBy(k => k).ToList();
}
