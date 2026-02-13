using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Tests.Unit;

public class ConditionExpressionParserTests
{
    [Fact]
    public void ParseCondition_SimpleComparison_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :minPrice");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(">", comp.Operator);

        var left = Assert.IsType<PathNode>(comp.Left);
        Assert.Single(left.Path.Elements);
        Assert.Equal("price", ((AttributeElement)left.Path.Elements[0]).Name);

        var right = Assert.IsType<ValuePlaceholderNode>(comp.Right);
        Assert.Equal(":minPrice", right.Placeholder);
    }

    [Fact]
    public void ParseCondition_Equals_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status = :val");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("=", comp.Operator);
    }

    [Fact]
    public void ParseCondition_NotEquals_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status <> :val");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("<>", comp.Operator);
    }

    [Fact]
    public void ParseCondition_LessThan_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age < :maxAge");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("<", comp.Operator);
    }

    [Fact]
    public void ParseCondition_LessThanOrEqual_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age <= :maxAge");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal("<=", comp.Operator);
    }

    [Fact]
    public void ParseCondition_GreaterThanOrEqual_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age >= :minAge");

        var comp = Assert.IsType<ComparisonNode>(result);
        Assert.Equal(">=", comp.Operator);
    }

    [Fact]
    public void ParseCondition_AndOperator_ReturnsLogicalNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :min AND status = :active");

        var logical = Assert.IsType<LogicalNode>(result);
        Assert.Equal("AND", logical.Operator);
        Assert.IsType<ComparisonNode>(logical.Left);
        Assert.IsType<ComparisonNode>(logical.Right);
    }

    [Fact]
    public void ParseCondition_OrOperator_ReturnsLogicalNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :min OR status = :active");

        var logical = Assert.IsType<LogicalNode>(result);
        Assert.Equal("OR", logical.Operator);
    }

    [Fact]
    public void ParseCondition_NotOperator_ReturnsNotNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("NOT contains(tags, :val)");

        var notNode = Assert.IsType<NotNode>(result);
        var func = Assert.IsType<FunctionNode>(notNode.Operand);
        Assert.Equal("contains", func.FunctionName);
    }

    [Fact]
    public void ParseCondition_Between_ReturnsBetweenNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age BETWEEN :low AND :high");

        var between = Assert.IsType<BetweenNode>(result);
        Assert.IsType<PathNode>(between.Value);
        Assert.IsType<ValuePlaceholderNode>(between.Low);
        Assert.IsType<ValuePlaceholderNode>(between.High);
    }

    [Fact]
    public void ParseCondition_In_ReturnsInNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status IN (:s1, :s2, :s3)");

        var inNode = Assert.IsType<InNode>(result);
        Assert.IsType<PathNode>(inNode.Value);
        Assert.Equal(3, inNode.List.Count);
        Assert.All(inNode.List, item => Assert.IsType<ValuePlaceholderNode>(item));
    }

    [Fact]
    public void ParseCondition_ParenthesizedGrouping_RespectsParentheses()
    {
        var result = DynamoDbExpressionParser.ParseCondition("(a = :v1 OR b = :v2) AND c = :v3");

        var and = Assert.IsType<LogicalNode>(result);
        Assert.Equal("AND", and.Operator);

        var or = Assert.IsType<LogicalNode>(and.Left);
        Assert.Equal("OR", or.Operator);

        Assert.IsType<ComparisonNode>(and.Right);
    }

    [Fact]
    public void ParseCondition_AttributeExists_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_exists(email)");

        var func = Assert.IsType<FunctionNode>(result);
        Assert.Equal("attribute_exists", func.FunctionName);
        Assert.Single(func.Arguments);
        Assert.IsType<PathNode>(func.Arguments[0]);
    }

    [Fact]
    public void ParseCondition_AttributeNotExists_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_not_exists(deleted)");

        var func = Assert.IsType<FunctionNode>(result);
        Assert.Equal("attribute_not_exists", func.FunctionName);
    }

    [Fact]
    public void ParseCondition_AttributeType_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_type(field, :type)");

        var func = Assert.IsType<FunctionNode>(result);
        Assert.Equal("attribute_type", func.FunctionName);
        Assert.Equal(2, func.Arguments.Count);
    }

    [Fact]
    public void ParseCondition_BeginsWith_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("begins_with(sk, :prefix)");

        var func = Assert.IsType<FunctionNode>(result);
        Assert.Equal("begins_with", func.FunctionName);
        Assert.Equal(2, func.Arguments.Count);
    }

    [Fact]
    public void ParseCondition_Contains_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("contains(description, :word)");

        var func = Assert.IsType<FunctionNode>(result);
        Assert.Equal("contains", func.FunctionName);
    }

    [Fact]
    public void ParseCondition_SizeInComparison_ReturnsComparisonWithFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("size(items) > :maxSize");

        var comp = Assert.IsType<ComparisonNode>(result);
        var sizeFunc = Assert.IsType<FunctionNode>(comp.Left);
        Assert.Equal("size", sizeFunc.FunctionName);
    }

    [Fact]
    public void ParseCondition_DocumentPathWithDots_ResolvesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("user.address.city = :city");

        var comp = Assert.IsType<ComparisonNode>(result);
        var path = Assert.IsType<PathNode>(comp.Left);
        Assert.Equal(3, path.Path.Elements.Count);
        Assert.Equal("user", ((AttributeElement)path.Path.Elements[0]).Name);
        Assert.Equal("address", ((AttributeElement)path.Path.Elements[1]).Name);
        Assert.Equal("city", ((AttributeElement)path.Path.Elements[2]).Name);
    }

    [Fact]
    public void ParseCondition_DocumentPathWithListIndex_ResolvesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("items[0].name = :val");

        var comp = Assert.IsType<ComparisonNode>(result);
        var path = Assert.IsType<PathNode>(comp.Left);
        Assert.Equal(3, path.Path.Elements.Count);
        Assert.Equal("items", ((AttributeElement)path.Path.Elements[0]).Name);
        Assert.Equal(0, ((IndexElement)path.Path.Elements[1]).Index);
        Assert.Equal("name", ((AttributeElement)path.Path.Elements[2]).Name);
    }

    [Fact]
    public void ParseCondition_ExpressionAttributeNames_ResolvesPlaceholders()
    {
        var names = new Dictionary<string, string> { { "#s", "status" } };

        var result = DynamoDbExpressionParser.ParseCondition("#s = :val", names);

        var comp = Assert.IsType<ComparisonNode>(result);
        var path = Assert.IsType<PathNode>(comp.Left);
        Assert.Equal("status", ((AttributeElement)path.Path.Elements[0]).Name);
    }

    [Fact]
    public void ParseCondition_NestedNamePlaceholders_ResolveCorrectly()
    {
        var names = new Dictionary<string, string>
        {
            { "#u", "user" },
            { "#n", "name" }
        };

        var result = DynamoDbExpressionParser.ParseCondition("#u.#n = :val", names);

        var comp = Assert.IsType<ComparisonNode>(result);
        var path = Assert.IsType<PathNode>(comp.Left);
        Assert.Equal(2, path.Path.Elements.Count);
        Assert.Equal("user", ((AttributeElement)path.Path.Elements[0]).Name);
        Assert.Equal("name", ((AttributeElement)path.Path.Elements[1]).Name);
    }

    [Fact]
    public void ParseCondition_MalformedExpression_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            DynamoDbExpressionParser.ParseCondition("AND price = :val"));
    }

    [Fact]
    public void ParseCondition_ComplexNestedExpression_ParsesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition(
            "attribute_exists(pk) AND (price > :min OR price < :max) AND NOT contains(tags, :tag)");

        var and1 = Assert.IsType<LogicalNode>(result);
        Assert.Equal("AND", and1.Operator);
    }

    [Fact]
    public void ParseCondition_CaseInsensitiveKeywords_ParsesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("a = :v1 and b = :v2 or c = :v3");

        // Should parse as: (a = :v1 AND b = :v2) OR c = :v3
        var or = Assert.IsType<LogicalNode>(result);
        Assert.Equal("OR", or.Operator);
    }
}
