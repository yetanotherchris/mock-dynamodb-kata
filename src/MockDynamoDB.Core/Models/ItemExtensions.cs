namespace MockDynamoDB.Core.Models;

internal static class ItemExtensions
{
    internal static Dictionary<string, AttributeValue> CloneItem(
        this Dictionary<string, AttributeValue> item)
    {
        return item.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone());
    }
}
