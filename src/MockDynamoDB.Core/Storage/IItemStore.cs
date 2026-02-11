using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Storage;

public interface IItemStore
{
    void PutItem(string tableName, Dictionary<string, AttributeValue> item);
    Dictionary<string, AttributeValue>? GetItem(string tableName, Dictionary<string, AttributeValue> key);
    Dictionary<string, AttributeValue>? DeleteItem(string tableName, Dictionary<string, AttributeValue> key);
    List<Dictionary<string, AttributeValue>> GetAllItems(string tableName);
    List<Dictionary<string, AttributeValue>> QueryByPartitionKey(string tableName, string pkName, AttributeValue pkValue);
    void EnsureTable(string tableName);
    void RemoveTable(string tableName);
}
