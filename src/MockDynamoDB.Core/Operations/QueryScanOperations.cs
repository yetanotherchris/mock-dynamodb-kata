using System.Text.Json;
using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;
using MockDynamoDB.Core.Storage;

namespace MockDynamoDB.Core.Operations;

public sealed class QueryScanOperations(ITableStore tableStore, IItemStore itemStore)
{
    public QueryResponse Query(QueryRequest request)
    {
        var table = tableStore.GetTable(request.TableName);

        // Check for index query
        LocalSecondaryIndexDefinition? lsiDef = null;
        GlobalSecondaryIndexDefinition? gsiDef = null;
        if (request.IndexName != null)
        {
            lsiDef = table.LocalSecondaryIndexes?.FirstOrDefault(l => l.IndexName == request.IndexName);
            if (lsiDef == null)
                gsiDef = table.GlobalSecondaryIndexes?.FirstOrDefault(g => g.IndexName == request.IndexName);
            if (lsiDef == null && gsiDef == null)
                throw new ValidationException($"The table does not have the specified index: {request.IndexName}");
        }

        var effectiveHashKeyName = gsiDef?.HashKeyName ?? table.HashKeyName;
        var effectiveRangeKeyName = lsiDef?.RangeKeyName ?? gsiDef?.RangeKeyName ?? table.RangeKeyName;

        // Parse KeyConditionExpression (expression format) or KeyConditions (pre-expression format)
        AttributeValue pkValue;
        SortKeyCondition? skCondition;

        if (request.KeyConditionExpression != null)
        {
            (pkValue, skCondition) = ParseKeyCondition(request.KeyConditionExpression,
                request.ExpressionAttributeNames, request.ExpressionAttributeValues,
                effectiveHashKeyName, effectiveRangeKeyName);
        }
        else if (request.KeyConditions is JsonElement keyConditions)
        {
            (pkValue, skCondition) = PreExpressionRequestParser.ParseKeyConditions(keyConditions, effectiveHashKeyName, effectiveRangeKeyName);
        }
        else
        {
            throw new ValidationException("Either the KeyConditions or KeyConditionExpression parameter must be specified");
        }

        // Get items by partition key
        List<Dictionary<string, AttributeValue>> items;
        if (request.IndexName != null)
            items = itemStore.QueryByPartitionKeyOnIndex(request.TableName, request.IndexName, table.HashKeyName, pkValue);
        else
            items = itemStore.QueryByPartitionKey(request.TableName, table.HashKeyName, pkValue);

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
        bool scanForward = request.ScanIndexForward ?? true;
        if (!scanForward)
            items.Reverse();

        // Pagination: ExclusiveStartKey
        if (request.ExclusiveStartKey != null)
        {
            var startIndex = FindExclusiveStartIndex(items, request.ExclusiveStartKey, table);
            if (startIndex >= 0)
                items = items.Skip(startIndex + 1).ToList();
            else
                items = [];
        }

        // Limit (applied before filter)
        int scannedCount;
        bool hasMore = false;

        if (request.Limit.HasValue && items.Count > request.Limit.Value)
        {
            items = items.Take(request.Limit.Value).ToList();
            hasMore = true;
        }
        scannedCount = items.Count;

        // FilterExpression (expression format) or QueryFilter (pre-expression format)
        if (request.FilterExpression != null)
        {
            var ast = DynamoDbExpressionParser.ParseCondition(request.FilterExpression, request.ExpressionAttributeNames);
            var evaluator = new ConditionEvaluator(request.ExpressionAttributeValues);
            items = items.Where(item => evaluator.Evaluate(ast, item)).ToList();
        }
        else if (request.QueryFilter is JsonElement qf)
        {
            bool useOr = request.ConditionalOperator == "OR";
            var predicate = PreExpressionRequestParser.ParseFilterConditions(qf, useOr);
            items = items.Where(predicate).ToList();
        }

        // ProjectionExpression
        if (request.ProjectionExpression != null)
            items = items.Select(item => ItemOperations.ApplyProjection(item, request.ProjectionExpression, request.ExpressionAttributeNames)).ToList();

        // Build LastEvaluatedKey
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
        if (hasMore && items.Count > 0)
        {
            var lastItem = items[^1];
            lastEvaluatedKey = ItemOperations.ExtractKey(lastItem, table);
            if (lsiDef != null && lastItem.TryGetValue(lsiDef.RangeKeyName, out var lsiSkVal))
                lastEvaluatedKey[lsiDef.RangeKeyName] = lsiSkVal;
            if (gsiDef != null)
            {
                if (lastItem.TryGetValue(gsiDef.HashKeyName, out var gsiHkVal))
                    lastEvaluatedKey[gsiDef.HashKeyName] = gsiHkVal;
                if (gsiDef.RangeKeyName != null && lastItem.TryGetValue(gsiDef.RangeKeyName, out var gsiSkVal))
                    lastEvaluatedKey[gsiDef.RangeKeyName] = gsiSkVal;
            }
        }

        // ConsumedCapacity
        ConsumedCapacityDto? consumedCapacity = null;
        if (request.ReturnConsumedCapacity is "TOTAL" or "INDEXES")
        {
            consumedCapacity = new ConsumedCapacityDto
            {
                TableName = request.TableName,
                CapacityUnits = 1.0
            };
        }

        return new QueryResponse
        {
            Items = request.Select != "COUNT" ? items : null,
            Count = items.Count,
            ScannedCount = scannedCount,
            LastEvaluatedKey = lastEvaluatedKey,
            ConsumedCapacity = consumedCapacity
        };
    }

    public ScanResponse Scan(ScanRequest request)
    {
        var table = tableStore.GetTable(request.TableName);

        var items = itemStore.GetAllItems(request.TableName);

        // Parallel scan: Segment / TotalSegments
        if (request.TotalSegments.HasValue)
        {
            var totalSegments = request.TotalSegments.Value;
            var segment = request.Segment!.Value;
            items = items.Where(item =>
            {
                var pkValue = item[table.HashKeyName];
                var hash = Fnv1aHash(InMemoryItemStore.GetAttributeKeyString(pkValue));
                return ((hash % (uint)totalSegments) == (uint)segment);
            }).ToList();
        }

        // ExclusiveStartKey
        if (request.ExclusiveStartKey != null)
        {
            var startIndex = FindExclusiveStartIndex(items, request.ExclusiveStartKey, table);
            if (startIndex >= 0)
                items = items.Skip(startIndex + 1).ToList();
            else
                items = [];
        }

        // Limit
        bool hasMore = false;
        if (request.Limit.HasValue && items.Count > request.Limit.Value)
        {
            items = items.Take(request.Limit.Value).ToList();
            hasMore = true;
        }
        int scannedCount = items.Count;

        // FilterExpression (expression format) or ScanFilter (pre-expression format)
        if (request.FilterExpression != null)
        {
            var ast = DynamoDbExpressionParser.ParseCondition(request.FilterExpression, request.ExpressionAttributeNames);
            var evaluator = new ConditionEvaluator(request.ExpressionAttributeValues);
            items = items.Where(item => evaluator.Evaluate(ast, item)).ToList();
        }
        else if (request.ScanFilter is JsonElement sf)
        {
            bool useOr = request.ConditionalOperator == "OR";
            var predicate = PreExpressionRequestParser.ParseFilterConditions(sf, useOr);
            items = items.Where(predicate).ToList();
        }

        // ProjectionExpression
        if (request.ProjectionExpression != null)
            items = items.Select(item => ItemOperations.ApplyProjection(item, request.ProjectionExpression, request.ExpressionAttributeNames)).ToList();

        // Build LastEvaluatedKey
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
        if (hasMore && items.Count > 0)
        {
            var lastItem = items[^1];
            lastEvaluatedKey = ItemOperations.ExtractKey(lastItem, table);
        }

        return new ScanResponse
        {
            Items = request.Select != "COUNT" ? items : null,
            Count = items.Count,
            ScannedCount = scannedCount,
            LastEvaluatedKey = lastEvaluatedKey
        };
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
}
