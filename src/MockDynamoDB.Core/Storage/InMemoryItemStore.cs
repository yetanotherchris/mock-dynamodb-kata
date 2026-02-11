using System.Collections.Concurrent;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Storage;

public class InMemoryItemStore : IItemStore
{
    private readonly ConcurrentDictionary<string, TableData> _tables = new();
    private readonly ITableStore _tableStore;

    public InMemoryItemStore(ITableStore tableStore)
    {
        _tableStore = tableStore;
    }

    public void EnsureTable(string tableName)
    {
        _tables.TryAdd(tableName, new TableData());
    }

    public void RemoveTable(string tableName)
    {
        _tables.TryRemove(tableName, out _);
    }

    public void PutItem(string tableName, Dictionary<string, AttributeValue> item)
    {
        var table = _tableStore.GetTable(tableName);
        var data = GetTableData(tableName);
        var pkValue = GetKeyString(item, table.HashKeyName);
        var skValue = table.HasRangeKey ? GetKeyString(item, table.RangeKeyName!) : "";

        var partition = data.Partitions.GetOrAdd(pkValue, _ => new SortedList<string, Dictionary<string, AttributeValue>>(StringComparer.Ordinal));

        lock (partition)
        {
            var cloned = CloneItem(item);
            partition[skValue] = cloned;
        }

        UpdateTableMetrics(tableName);
    }

    public Dictionary<string, AttributeValue>? GetItem(string tableName, Dictionary<string, AttributeValue> key)
    {
        var table = _tableStore.GetTable(tableName);
        var data = GetTableData(tableName);
        var pkValue = GetKeyString(key, table.HashKeyName);
        var skValue = table.HasRangeKey ? GetKeyString(key, table.RangeKeyName!) : "";

        if (!data.Partitions.TryGetValue(pkValue, out var partition))
            return null;

        lock (partition)
        {
            return partition.TryGetValue(skValue, out var item) ? CloneItem(item) : null;
        }
    }

    public Dictionary<string, AttributeValue>? DeleteItem(string tableName, Dictionary<string, AttributeValue> key)
    {
        var table = _tableStore.GetTable(tableName);
        var data = GetTableData(tableName);
        var pkValue = GetKeyString(key, table.HashKeyName);
        var skValue = table.HasRangeKey ? GetKeyString(key, table.RangeKeyName!) : "";

        if (!data.Partitions.TryGetValue(pkValue, out var partition))
            return null;

        lock (partition)
        {
            if (!partition.TryGetValue(skValue, out var existing))
                return null;

            partition.Remove(skValue);

            if (partition.Count == 0)
                data.Partitions.TryRemove(pkValue, out _);

            UpdateTableMetrics(tableName);
            return CloneItem(existing);
        }
    }

    public List<Dictionary<string, AttributeValue>> GetAllItems(string tableName)
    {
        var data = GetTableData(tableName);
        var result = new List<Dictionary<string, AttributeValue>>();

        foreach (var pkEntry in data.Partitions.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            lock (pkEntry.Value)
            {
                foreach (var item in pkEntry.Value.Values)
                {
                    result.Add(CloneItem(item));
                }
            }
        }

        return result;
    }

    public List<Dictionary<string, AttributeValue>> QueryByPartitionKey(string tableName, string pkName, AttributeValue pkValue)
    {
        var data = GetTableData(tableName);
        var pkString = GetAttributeKeyString(pkValue);

        if (!data.Partitions.TryGetValue(pkString, out var partition))
            return [];

        lock (partition)
        {
            return partition.Values.Select(CloneItem).ToList();
        }
    }

    private TableData GetTableData(string tableName)
    {
        if (!_tables.TryGetValue(tableName, out var data))
        {
            data = new TableData();
            _tables.TryAdd(tableName, data);
            if (!_tables.TryGetValue(tableName, out data))
                data = new TableData();
        }
        return data;
    }

    private void UpdateTableMetrics(string tableName)
    {
        try
        {
            var table = _tableStore.GetTable(tableName);
            var data = GetTableData(tableName);
            long count = 0;
            foreach (var partition in data.Partitions.Values)
            {
                lock (partition)
                {
                    count += partition.Count;
                }
            }
            table.ItemCount = count;
        }
        catch (ResourceNotFoundException)
        {
            // table may have been deleted
        }
    }

    internal static string GetKeyString(Dictionary<string, AttributeValue> item, string keyName)
    {
        if (!item.TryGetValue(keyName, out var value))
            throw new ValidationException($"One or more parameter values are not valid. The AttributeValue for a key attribute cannot contain an empty string value. Key: {keyName}");

        return GetAttributeKeyString(value);
    }

    internal static string GetAttributeKeyString(AttributeValue value)
    {
        return value.Type switch
        {
            AttributeValueType.S => $"S:{value.S}",
            AttributeValueType.N => $"N:{NormalizeNumber(value.N!)}",
            AttributeValueType.B => $"B:{value.B}",
            _ => throw new ValidationException("Invalid key attribute type")
        };
    }

    private static string NormalizeNumber(string n)
    {
        if (decimal.TryParse(n, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return n;
    }

    private static Dictionary<string, AttributeValue> CloneItem(Dictionary<string, AttributeValue> item)
    {
        return item.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone());
    }

    private class TableData
    {
        public ConcurrentDictionary<string, SortedList<string, Dictionary<string, AttributeValue>>> Partitions { get; } = new();
    }
}
