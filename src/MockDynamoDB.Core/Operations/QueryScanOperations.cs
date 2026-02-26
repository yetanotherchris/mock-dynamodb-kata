using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public class QueryScanOperations
{
    private readonly ITableStore _tableStore;
    private readonly IItemStore _itemStore;

    public QueryScanOperations(ITableStore tableStore, IItemStore itemStore)
    {
        _tableStore = tableStore;
        _itemStore = itemStore;
    }

    public JsonDocument Query(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var table = _tableStore.GetTable(tableName);

        // Check for index query
        LocalSecondaryIndexDefinition? lsiDef = null;
        GlobalSecondaryIndexDefinition? gsiDef = null;
        string? indexName = null;
        if (root.TryGetProperty("IndexName", out var idx))
        {
            indexName = idx.GetString();
            lsiDef = table.LocalSecondaryIndexes?.FirstOrDefault(l => l.IndexName == indexName);
            if (lsiDef == null)
                gsiDef = table.GlobalSecondaryIndexes?.FirstOrDefault(g => g.IndexName == indexName);
            if (lsiDef == null && gsiDef == null)
                throw new ValidationException($"The table does not have the specified index: {indexName}");
        }

        var effectiveHashKeyName = gsiDef?.HashKeyName ?? table.HashKeyName;
        var effectiveRangeKeyName = lsiDef?.RangeKeyName ?? gsiDef?.RangeKeyName ?? table.RangeKeyName;

        var expressionAttributeNames = root.TryGetProperty("ExpressionAttributeNames", out var ean)
            ? ItemOperations.DeserializeStringMap(ean) : null;
        var expressionAttributeValues = root.TryGetProperty("ExpressionAttributeValues", out var eav)
            ? ItemOperations.DeserializeItem(eav) : null;

        // Parse KeyConditionExpression
        if (!root.TryGetProperty("KeyConditionExpression", out var kce))
            throw new ValidationException("Either the KeyConditions or KeyConditionExpression parameter must be specified");

        var keyCondition = kce.GetString()!;
        var (pkValue, skCondition) = ParseKeyCondition(keyCondition, expressionAttributeNames, expressionAttributeValues, effectiveHashKeyName, effectiveRangeKeyName);

        // Get items by partition key
        List<Dictionary<string, AttributeValue>> items;
        if (indexName != null)
            items = _itemStore.QueryByPartitionKeyOnIndex(tableName, indexName, table.HashKeyName, pkValue);
        else
            items = _itemStore.QueryByPartitionKey(tableName, table.HashKeyName, pkValue);

        // Apply sort key condition
        if (skCondition != null && effectiveRangeKeyName != null)
        {
            items = items.Where(item =>
            {
                var sk = item.TryGetValue(effectiveRangeKeyName, out var v) ? v : null;
                return sk != null && EvaluateSortKeyCondition(sk, skCondition);
            }).ToList();
        }

        // ScanIndexForward
        bool scanForward = true;
        if (root.TryGetProperty("ScanIndexForward", out var sif))
            scanForward = sif.GetBoolean();
        if (!scanForward)
            items.Reverse();

        // Pagination: ExclusiveStartKey
        if (root.TryGetProperty("ExclusiveStartKey", out var esk))
        {
            var startKey = ItemOperations.DeserializeItem(esk);
            var startIndex = FindExclusiveStartIndex(items, startKey, table);
            if (startIndex >= 0)
                items = items.Skip(startIndex + 1).ToList();
            else
                items = [];
        }

        // Limit (applied before filter)
        int? limit = null;
        if (root.TryGetProperty("Limit", out var lim))
            limit = lim.GetInt32();

        int scannedCount;
        bool hasMore = false;

        if (limit.HasValue && items.Count > limit.Value)
        {
            items = items.Take(limit.Value).ToList();
            hasMore = true;
        }
        scannedCount = items.Count;

        // FilterExpression
        if (root.TryGetProperty("FilterExpression", out var fe))
        {
            var ast = DynamoDbExpressionParser.ParseCondition(fe.GetString()!, expressionAttributeNames);
            var evaluator = new ConditionEvaluator(expressionAttributeValues);
            items = items.Where(item => evaluator.Evaluate(ast, item)).ToList();
        }

        // Select = COUNT
        string? select = null;
        if (root.TryGetProperty("Select", out var sel))
            select = sel.GetString();

        // ProjectionExpression
        string? projectionExpression = null;
        if (root.TryGetProperty("ProjectionExpression", out var pe))
            projectionExpression = pe.GetString();

        if (projectionExpression != null)
            items = items.Select(item => ItemOperations.ApplyProjection(item, projectionExpression, expressionAttributeNames)).ToList();

        return BuildQueryScanResponse(items, scannedCount, hasMore ? items : null, table, select, lsiDef, gsiDef,
            tableName, root.TryGetProperty("ReturnConsumedCapacity", out var rcc) ? rcc.GetString() : null);
    }

    public JsonDocument Scan(JsonDocument request)
    {
        var root = request.RootElement;
        var tableName = root.GetProperty("TableName").GetString()!;
        var table = _tableStore.GetTable(tableName);

        var expressionAttributeNames = root.TryGetProperty("ExpressionAttributeNames", out var ean)
            ? ItemOperations.DeserializeStringMap(ean) : null;
        var expressionAttributeValues = root.TryGetProperty("ExpressionAttributeValues", out var eav)
            ? ItemOperations.DeserializeItem(eav) : null;

        var items = _itemStore.GetAllItems(tableName);

        // Parallel scan: Segment / TotalSegments
        if (root.TryGetProperty("TotalSegments", out var ts))
        {
            var totalSegments = ts.GetInt32();
            var segment = root.GetProperty("Segment").GetInt32();
            items = items.Where(item =>
            {
                var pkValue = item[table.HashKeyName];
                var hash = Fnv1aHash(InMemoryItemStore.GetAttributeKeyString(pkValue));
                return ((hash % (uint)totalSegments) == (uint)segment);
            }).ToList();
        }

        // ExclusiveStartKey
        if (root.TryGetProperty("ExclusiveStartKey", out var esk))
        {
            var startKey = ItemOperations.DeserializeItem(esk);
            var startIndex = FindExclusiveStartIndex(items, startKey, table);
            if (startIndex >= 0)
                items = items.Skip(startIndex + 1).ToList();
            else
                items = [];
        }

        // Limit
        int? limit = null;
        if (root.TryGetProperty("Limit", out var lim))
            limit = lim.GetInt32();

        bool hasMore = false;
        if (limit.HasValue && items.Count > limit.Value)
        {
            items = items.Take(limit.Value).ToList();
            hasMore = true;
        }
        int scannedCount = items.Count;

        // FilterExpression
        if (root.TryGetProperty("FilterExpression", out var fe))
        {
            var ast = DynamoDbExpressionParser.ParseCondition(fe.GetString()!, expressionAttributeNames);
            var evaluator = new ConditionEvaluator(expressionAttributeValues);
            items = items.Where(item => evaluator.Evaluate(ast, item)).ToList();
        }

        // Select
        string? select = null;
        if (root.TryGetProperty("Select", out var sel))
            select = sel.GetString();

        // ProjectionExpression
        string? projectionExpression = null;
        if (root.TryGetProperty("ProjectionExpression", out var pe))
            projectionExpression = pe.GetString();

        if (projectionExpression != null)
            items = items.Select(item => ItemOperations.ApplyProjection(item, projectionExpression, expressionAttributeNames)).ToList();

        return BuildQueryScanResponse(items, scannedCount, hasMore ? items : null, table, select);
    }

    private (AttributeValue pkValue, SortKeyCondition? skCondition) ParseKeyCondition(
        string expression,
        Dictionary<string, string>? expressionAttributeNames,
        Dictionary<string, AttributeValue>? expressionAttributeValues,
        string hashKeyName,
        string? rangeKeyName)
    {
        var ast = DynamoDbExpressionParser.ParseCondition(expression, expressionAttributeNames);

        AttributeValue? pkValue = null;
        SortKeyCondition? skCondition = null;

        ExtractKeyConditions(ast, hashKeyName, rangeKeyName, expressionAttributeValues, ref pkValue, ref skCondition);

        if (pkValue == null)
            throw new ValidationException("Query condition missed key schema element: " + hashKeyName);

        return (pkValue, skCondition);
    }

    private void ExtractKeyConditions(
        ExpressionNode node,
        string hashKeyName,
        string? rangeKeyName,
        Dictionary<string, AttributeValue>? expressionAttributeValues,
        ref AttributeValue? pkValue,
        ref SortKeyCondition? skCondition)
    {
        if (node is LogicalNode logical && logical.Operator == "AND")
        {
            ExtractKeyConditions(logical.Left, hashKeyName, rangeKeyName, expressionAttributeValues, ref pkValue, ref skCondition);
            ExtractKeyConditions(logical.Right, hashKeyName, rangeKeyName, expressionAttributeValues, ref pkValue, ref skCondition);
            return;
        }

        if (node is ComparisonNode comp)
        {
            var pathName = GetPathName(comp.Left);
            if (pathName == hashKeyName && comp.Operator == "=")
            {
                pkValue = ResolveExprValue(comp.Right, expressionAttributeValues);
                return;
            }

            if (pathName == rangeKeyName)
            {
                var val = ResolveExprValue(comp.Right, expressionAttributeValues);
                skCondition = new SortKeyCondition { Operator = comp.Operator, Value = val };
                return;
            }
        }

        if (node is BetweenNode between)
        {
            var pathName = GetPathName(between.Value);
            if (pathName == rangeKeyName)
            {
                var low = ResolveExprValue(between.Low, expressionAttributeValues);
                var high = ResolveExprValue(between.High, expressionAttributeValues);
                skCondition = new SortKeyCondition { Operator = "BETWEEN", Value = low, Value2 = high };
                return;
            }
        }

        if (node is FunctionNode func && func.FunctionName == "begins_with")
        {
            var pathName = GetPathName(func.Arguments[0]);
            if (pathName == rangeKeyName)
            {
                var prefix = ResolveExprValue(func.Arguments[1], expressionAttributeValues);
                skCondition = new SortKeyCondition { Operator = "begins_with", Value = prefix };
                return;
            }
        }
    }

    private string? GetPathName(ExpressionNode node)
    {
        if (node is PathNode pathNode && pathNode.Path.Elements.Count == 1 && pathNode.Path.Elements[0] is AttributeElement attr)
            return attr.Name;
        return null;
    }

    private AttributeValue ResolveExprValue(ExpressionNode node, Dictionary<string, AttributeValue>? values)
    {
        if (node is ValuePlaceholderNode placeholder)
        {
            if (values == null || !values.TryGetValue(placeholder.Placeholder, out var val))
                throw new ValidationException($"Value {placeholder.Placeholder} not found in ExpressionAttributeValues");
            return val;
        }
        throw new ValidationException("Expected a value placeholder in key condition");
    }

    private bool EvaluateSortKeyCondition(AttributeValue sk, SortKeyCondition condition)
    {
        var cmp = ConditionEvaluator.CompareValues(sk, condition.Value!);
        if (cmp == null) return false;

        return condition.Operator switch
        {
            "=" => cmp == 0,
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            "BETWEEN" =>
                ConditionEvaluator.CompareValues(sk, condition.Value!) >= 0 &&
                ConditionEvaluator.CompareValues(sk, condition.Value2!) <= 0,
            "begins_with" =>
                sk.S != null && condition.Value!.S != null &&
                sk.S.StartsWith(condition.Value.S, StringComparison.Ordinal),
            _ => false
        };
    }

    private int FindExclusiveStartIndex(List<Dictionary<string, AttributeValue>> items,
        Dictionary<string, AttributeValue> startKey, TableDefinition table)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            bool match = true;
            foreach (var kv in startKey)
            {
                if (!item.TryGetValue(kv.Key, out var v) || ConditionEvaluator.CompareValues(v, kv.Value) != 0)
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private JsonDocument BuildQueryScanResponse(
        List<Dictionary<string, AttributeValue>> items,
        int scannedCount,
        List<Dictionary<string, AttributeValue>>? lastPageItems,
        TableDefinition table,
        string? select,
        LocalSecondaryIndexDefinition? lsiDef = null,
        GlobalSecondaryIndexDefinition? gsiDef = null,
        string? tableName = null,
        string? returnConsumedCapacity = null)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        if (select != "COUNT")
        {
            writer.WritePropertyName("Items");
            ItemOperations.WriteItemsList(writer, items);
        }

        writer.WriteNumber("Count", items.Count);
        writer.WriteNumber("ScannedCount", scannedCount);

        // LastEvaluatedKey
        if (lastPageItems != null && lastPageItems.Count > 0)
        {
            var lastItem = lastPageItems[^1];
            writer.WritePropertyName("LastEvaluatedKey");
            var lastKey = ItemOperations.ExtractKey(lastItem, table);
            if (lsiDef != null && lastItem.TryGetValue(lsiDef.RangeKeyName, out var lsiSkVal))
                lastKey[lsiDef.RangeKeyName] = lsiSkVal;
            if (gsiDef != null)
            {
                if (lastItem.TryGetValue(gsiDef.HashKeyName, out var gsiHkVal))
                    lastKey[gsiDef.HashKeyName] = gsiHkVal;
                if (gsiDef.RangeKeyName != null && lastItem.TryGetValue(gsiDef.RangeKeyName, out var gsiSkVal))
                    lastKey[gsiDef.RangeKeyName] = gsiSkVal;
            }
            ItemOperations.WriteItem(writer, lastKey);
        }

        // ConsumedCapacity (returned when explicitly requested)
        if (tableName != null && returnConsumedCapacity is "TOTAL" or "INDEXES")
        {
            writer.WritePropertyName("ConsumedCapacity");
            writer.WriteStartObject();
            writer.WriteString("TableName", tableName);
            writer.WriteNumber("CapacityUnits", 1.0);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();
        return JsonDocument.Parse(stream.ToArray());
    }

    internal static uint Fnv1aHash(string data)
    {
        const uint fnvPrime = 0x01000193;
        const uint fnvOffset = 0x811c9dc5;
        uint hash = fnvOffset;
        foreach (char c in data)
        {
            hash ^= (byte)c;
            hash *= fnvPrime;
        }
        return hash;
    }

    private class SortKeyCondition
    {
        public string Operator { get; set; } = "";
        public AttributeValue? Value { get; set; }
        public AttributeValue? Value2 { get; set; } // for BETWEEN
    }
}
