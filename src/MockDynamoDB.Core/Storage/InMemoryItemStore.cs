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

        Dictionary<string, AttributeValue>? oldItem = null;
        lock (partition)
        {
            partition.TryGetValue(skValue, out oldItem);
            var cloned = item.CloneItem();
            partition[skValue] = cloned;
        }

        if (table.LocalSecondaryIndexes is { Count: > 0 })
        {
            foreach (var lsi in table.LocalSecondaryIndexes)
                UpdateLsiOnPut(data, lsi, pkValue, skValue, oldItem, item);
        }

        if (table.GlobalSecondaryIndexes is { Count: > 0 })
        {
            foreach (var gsi in table.GlobalSecondaryIndexes)
                UpdateGsiOnPut(data, gsi, pkValue, skValue, oldItem, item);
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
            return partition.TryGetValue(skValue, out var item) ? item.CloneItem() : null;
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

        Dictionary<string, AttributeValue>? existing;
        lock (partition)
        {
            if (!partition.TryGetValue(skValue, out existing))
                return null;

            partition.Remove(skValue);

            if (partition.Count == 0)
                data.Partitions.TryRemove(pkValue, out _);
        }

        if (table.LocalSecondaryIndexes is { Count: > 0 })
        {
            foreach (var lsi in table.LocalSecondaryIndexes)
                RemoveFromLsiIndex(data, lsi, pkValue, skValue, existing);
        }

        if (table.GlobalSecondaryIndexes is { Count: > 0 })
        {
            foreach (var gsi in table.GlobalSecondaryIndexes)
                RemoveFromGsiIndex(data, gsi, pkValue, skValue, existing);
        }

        UpdateTableMetrics(tableName);
        return existing.CloneItem();
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
                    result.Add(item.CloneItem());
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
            return partition.Values.Select(v => v.CloneItem()).ToList();
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

    public List<Dictionary<string, AttributeValue>> QueryByPartitionKeyOnIndex(
        string tableName, string indexName, string pkName, AttributeValue pkValue)
    {
        var data = GetTableData(tableName);
        var pkString = GetAttributeKeyString(pkValue);

        if (!data.Indexes.TryGetValue(indexName, out var indexData))
            return [];

        if (!indexData.TryGetValue(pkString, out var partition))
            return [];

        lock (partition)
        {
            return partition.Values.Select(v => v.CloneItem()).ToList();
        }
    }

    private void UpdateLsiOnPut(
        TableData data,
        LocalSecondaryIndexDefinition lsi,
        string pkString,
        string tableSkString,
        Dictionary<string, AttributeValue>? oldItem,
        Dictionary<string, AttributeValue> newItem)
    {
        var indexData = data.Indexes.GetOrAdd(lsi.IndexName,
            _ => new ConcurrentDictionary<string, SortedList<string, Dictionary<string, AttributeValue>>>());
        var lsiSkName = lsi.RangeKeyName;

        // Remove old entry
        if (oldItem != null && oldItem.TryGetValue(lsiSkName, out var oldLsiSk))
        {
            var oldCompositeKey = GetAttributeKeyString(oldLsiSk) + "\0" + tableSkString;
            if (indexData.TryGetValue(pkString, out var oldPartition))
            {
                lock (oldPartition)
                {
                    oldPartition.Remove(oldCompositeKey);
                    if (oldPartition.Count == 0)
                        indexData.TryRemove(pkString, out _);
                }
            }
        }

        // Add new entry (only if item has the LSI sort key)
        if (newItem.TryGetValue(lsiSkName, out var newLsiSk))
        {
            var compositeKey = GetAttributeKeyString(newLsiSk) + "\0" + tableSkString;
            var partition = indexData.GetOrAdd(pkString,
                _ => new SortedList<string, Dictionary<string, AttributeValue>>(StringComparer.Ordinal));
            lock (partition)
            {
                partition[compositeKey] = newItem.CloneItem();
            }
        }
    }

    private void RemoveFromLsiIndex(
        TableData data,
        LocalSecondaryIndexDefinition lsi,
        string pkString,
        string tableSkString,
        Dictionary<string, AttributeValue> item)
    {
        var lsiSkName = lsi.RangeKeyName;
        if (!item.TryGetValue(lsiSkName, out var lsiSk))
            return;

        if (!data.Indexes.TryGetValue(lsi.IndexName, out var indexData))
            return;

        var compositeKey = GetAttributeKeyString(lsiSk) + "\0" + tableSkString;
        if (indexData.TryGetValue(pkString, out var partition))
        {
            lock (partition)
            {
                partition.Remove(compositeKey);
                if (partition.Count == 0)
                    indexData.TryRemove(pkString, out _);
            }
        }
    }

    private void UpdateGsiOnPut(
        TableData data,
        GlobalSecondaryIndexDefinition gsi,
        string tablePkString,
        string tableSkString,
        Dictionary<string, AttributeValue>? oldItem,
        Dictionary<string, AttributeValue> newItem)
    {
        var gsiHashKeyName = gsi.HashKeyName;
        var gsiSkName = gsi.RangeKeyName;

        var indexData = data.Indexes.GetOrAdd(gsi.IndexName,
            _ => new ConcurrentDictionary<string, SortedList<string, Dictionary<string, AttributeValue>>>());

        // Remove old entry
        if (oldItem != null && oldItem.TryGetValue(gsiHashKeyName, out var oldGsiHashKey))
        {
            var oldGsiPkString = GetAttributeKeyString(oldGsiHashKey);
            var oldCompositeKey = BuildGsiCompositeKey(oldItem, gsiSkName, tablePkString, tableSkString);
            if (indexData.TryGetValue(oldGsiPkString, out var oldPartition))
            {
                lock (oldPartition)
                {
                    oldPartition.Remove(oldCompositeKey);
                    if (oldPartition.Count == 0)
                        indexData.TryRemove(oldGsiPkString, out _);
                }
            }
        }

        // Add new entry (only if item has the GSI hash key)
        if (newItem.TryGetValue(gsiHashKeyName, out var newGsiHashKey))
        {
            var gsiPkString = GetAttributeKeyString(newGsiHashKey);
            var compositeKey = BuildGsiCompositeKey(newItem, gsiSkName, tablePkString, tableSkString);
            var partition = indexData.GetOrAdd(gsiPkString,
                _ => new SortedList<string, Dictionary<string, AttributeValue>>(StringComparer.Ordinal));
            lock (partition)
            {
                partition[compositeKey] = newItem.CloneItem();
            }
        }
    }

    private static string BuildGsiCompositeKey(
        Dictionary<string, AttributeValue> item,
        string? gsiSkName,
        string tablePkString,
        string tableSkString)
    {
        // Include table primary key to guarantee uniqueness within a GSI partition
        if (gsiSkName != null && item.TryGetValue(gsiSkName, out var gsiSk))
            return GetAttributeKeyString(gsiSk) + "\0" + tablePkString + "\0" + tableSkString;
        return tablePkString + "\0" + tableSkString;
    }

    private void RemoveFromGsiIndex(
        TableData data,
        GlobalSecondaryIndexDefinition gsi,
        string tablePkString,
        string tableSkString,
        Dictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue(gsi.HashKeyName, out var gsiHashKey))
            return;

        if (!data.Indexes.TryGetValue(gsi.IndexName, out var indexData))
            return;

        var gsiPkString = GetAttributeKeyString(gsiHashKey);
        var compositeKey = BuildGsiCompositeKey(item, gsi.RangeKeyName, tablePkString, tableSkString);
        if (indexData.TryGetValue(gsiPkString, out var partition))
        {
            lock (partition)
            {
                partition.Remove(compositeKey);
                if (partition.Count == 0)
                    indexData.TryRemove(gsiPkString, out _);
            }
        }
    }

    private class TableData
    {
        public ConcurrentDictionary<string, SortedList<string, Dictionary<string, AttributeValue>>> Partitions { get; } = new();
        public ConcurrentDictionary<string, ConcurrentDictionary<string, SortedList<string, Dictionary<string, AttributeValue>>>> Indexes { get; } = new();
    }
}
