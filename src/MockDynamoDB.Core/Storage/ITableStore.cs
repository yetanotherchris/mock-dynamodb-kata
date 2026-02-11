using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Storage;

public interface ITableStore
{
    void CreateTable(TableDefinition table);
    TableDefinition GetTable(string tableName);
    TableDefinition DeleteTable(string tableName);
    bool TableExists(string tableName);
    List<string> ListTableNames();
}
