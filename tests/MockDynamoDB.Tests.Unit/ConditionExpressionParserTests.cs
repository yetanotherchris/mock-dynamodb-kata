using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Tests.Unit;

public class ConditionExpressionParserTests
{
    [Test]
    public async Task ParseCondition_SimpleComparison_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :minPrice");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo(">");

        await Assert.That(comp.Left).IsTypeOf<PathNode>();
        var left = (PathNode)comp.Left;
        await Assert.That(left.Path.Elements).HasCount().EqualTo(1);
        await Assert.That(((AttributeElement)left.Path.Elements[0]).Name).IsEqualTo("price");

        await Assert.That(comp.Right).IsTypeOf<ValuePlaceholderNode>();
        var right = (ValuePlaceholderNode)comp.Right;
        await Assert.That(right.Placeholder).IsEqualTo(":minPrice");
    }

    [Test]
    public async Task ParseCondition_Equals_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status = :val");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo("=");
    }

    [Test]
    public async Task ParseCondition_NotEquals_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status <> :val");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo("<>");
    }

    [Test]
    public async Task ParseCondition_LessThan_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age < :maxAge");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo("<");
    }

    [Test]
    public async Task ParseCondition_LessThanOrEqual_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age <= :maxAge");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo("<=");
    }

    [Test]
    public async Task ParseCondition_GreaterThanOrEqual_ReturnsComparisonNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age >= :minAge");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Operator).IsEqualTo(">=");
    }

    [Test]
    public async Task ParseCondition_AndOperator_ReturnsLogicalNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :min AND status = :active");

        await Assert.That(result).IsTypeOf<LogicalNode>();
        var logical = (LogicalNode)result;
        await Assert.That(logical.Operator).IsEqualTo("AND");
        await Assert.That(logical.Left).IsTypeOf<ComparisonNode>();
        await Assert.That(logical.Right).IsTypeOf<ComparisonNode>();
    }

    [Test]
    public async Task ParseCondition_OrOperator_ReturnsLogicalNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("price > :min OR status = :active");

        await Assert.That(result).IsTypeOf<LogicalNode>();
        var logical = (LogicalNode)result;
        await Assert.That(logical.Operator).IsEqualTo("OR");
    }

    [Test]
    public async Task ParseCondition_NotOperator_ReturnsNotNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("NOT contains(tags, :val)");

        await Assert.That(result).IsTypeOf<NotNode>();
        var notNode = (NotNode)result;
        await Assert.That(notNode.Operand).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)notNode.Operand;
        await Assert.That(func.FunctionName).IsEqualTo("contains");
    }

    [Test]
    public async Task ParseCondition_Between_ReturnsBetweenNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("age BETWEEN :low AND :high");

        await Assert.That(result).IsTypeOf<BetweenNode>();
        var between = (BetweenNode)result;
        await Assert.That(between.Value).IsTypeOf<PathNode>();
        await Assert.That(between.Low).IsTypeOf<ValuePlaceholderNode>();
        await Assert.That(between.High).IsTypeOf<ValuePlaceholderNode>();
    }

    [Test]
    public async Task ParseCondition_In_ReturnsInNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("status IN (:s1, :s2, :s3)");

        await Assert.That(result).IsTypeOf<InNode>();
        var inNode = (InNode)result;
        await Assert.That(inNode.Value).IsTypeOf<PathNode>();
        await Assert.That(inNode.List).HasCount().EqualTo(3);
        foreach (var item in inNode.List)
        {
            await Assert.That(item).IsTypeOf<ValuePlaceholderNode>();
        }
    }

    [Test]
    public async Task ParseCondition_ParenthesizedGrouping_RespectsParentheses()
    {
        var result = DynamoDbExpressionParser.ParseCondition("(a = :v1 OR b = :v2) AND c = :v3");

        await Assert.That(result).IsTypeOf<LogicalNode>();
        var and = (LogicalNode)result;
        await Assert.That(and.Operator).IsEqualTo("AND");

        await Assert.That(and.Left).IsTypeOf<LogicalNode>();
        var or = (LogicalNode)and.Left;
        await Assert.That(or.Operator).IsEqualTo("OR");

        await Assert.That(and.Right).IsTypeOf<ComparisonNode>();
    }

    [Test]
    public async Task ParseCondition_AttributeExists_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_exists(email)");

        await Assert.That(result).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)result;
        await Assert.That(func.FunctionName).IsEqualTo("attribute_exists");
        await Assert.That(func.Arguments).HasCount().EqualTo(1);
        await Assert.That(func.Arguments[0]).IsTypeOf<PathNode>();
    }

    [Test]
    public async Task ParseCondition_AttributeNotExists_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_not_exists(deleted)");

        await Assert.That(result).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)result;
        await Assert.That(func.FunctionName).IsEqualTo("attribute_not_exists");
    }

    [Test]
    public async Task ParseCondition_AttributeType_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("attribute_type(field, :type)");

        await Assert.That(result).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)result;
        await Assert.That(func.FunctionName).IsEqualTo("attribute_type");
        await Assert.That(func.Arguments).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ParseCondition_BeginsWith_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("begins_with(sk, :prefix)");

        await Assert.That(result).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)result;
        await Assert.That(func.FunctionName).IsEqualTo("begins_with");
        await Assert.That(func.Arguments).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ParseCondition_Contains_ReturnsFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("contains(description, :word)");

        await Assert.That(result).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)result;
        await Assert.That(func.FunctionName).IsEqualTo("contains");
    }

    [Test]
    public async Task ParseCondition_SizeInComparison_ReturnsComparisonWithFunctionNode()
    {
        var result = DynamoDbExpressionParser.ParseCondition("size(items) > :maxSize");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Left).IsTypeOf<FunctionNode>();
        var sizeFunc = (FunctionNode)comp.Left;
        await Assert.That(sizeFunc.FunctionName).IsEqualTo("size");
    }

    [Test]
    public async Task ParseCondition_DocumentPathWithDots_ResolvesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("user.address.city = :city");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Left).IsTypeOf<PathNode>();
        var path = (PathNode)comp.Left;
        await Assert.That(path.Path.Elements).HasCount().EqualTo(3);
        await Assert.That(((AttributeElement)path.Path.Elements[0]).Name).IsEqualTo("user");
        await Assert.That(((AttributeElement)path.Path.Elements[1]).Name).IsEqualTo("address");
        await Assert.That(((AttributeElement)path.Path.Elements[2]).Name).IsEqualTo("city");
    }

    [Test]
    public async Task ParseCondition_DocumentPathWithListIndex_ResolvesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("items[0].name = :val");

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Left).IsTypeOf<PathNode>();
        var path = (PathNode)comp.Left;
        await Assert.That(path.Path.Elements).HasCount().EqualTo(3);
        await Assert.That(((AttributeElement)path.Path.Elements[0]).Name).IsEqualTo("items");
        await Assert.That(((IndexElement)path.Path.Elements[1]).Index).IsEqualTo(0);
        await Assert.That(((AttributeElement)path.Path.Elements[2]).Name).IsEqualTo("name");
    }

    [Test]
    public async Task ParseCondition_ExpressionAttributeNames_ResolvesPlaceholders()
    {
        var names = new Dictionary<string, string> { { "#s", "status" } };

        var result = DynamoDbExpressionParser.ParseCondition("#s = :val", names);

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Left).IsTypeOf<PathNode>();
        var path = (PathNode)comp.Left;
        await Assert.That(((AttributeElement)path.Path.Elements[0]).Name).IsEqualTo("status");
    }

    [Test]
    public async Task ParseCondition_NestedNamePlaceholders_ResolveCorrectly()
    {
        var names = new Dictionary<string, string>
        {
            { "#u", "user" },
            { "#n", "name" }
        };

        var result = DynamoDbExpressionParser.ParseCondition("#u.#n = :val", names);

        await Assert.That(result).IsTypeOf<ComparisonNode>();
        var comp = (ComparisonNode)result;
        await Assert.That(comp.Left).IsTypeOf<PathNode>();
        var path = (PathNode)comp.Left;
        await Assert.That(path.Path.Elements).HasCount().EqualTo(2);
        await Assert.That(((AttributeElement)path.Path.Elements[0]).Name).IsEqualTo("user");
        await Assert.That(((AttributeElement)path.Path.Elements[1]).Name).IsEqualTo("name");
    }

    [Test]
    public async Task ParseCondition_MalformedExpression_ThrowsValidationException()
    {
        await Assert.That(() =>
            DynamoDbExpressionParser.ParseCondition("AND price = :val"))
            .ThrowsExactly<ValidationException>();
    }

    [Test]
    public async Task ParseCondition_ComplexNestedExpression_ParsesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition(
            "attribute_exists(pk) AND (price > :min OR price < :max) AND NOT contains(tags, :tag)");

        await Assert.That(result).IsTypeOf<LogicalNode>();
        var and1 = (LogicalNode)result;
        await Assert.That(and1.Operator).IsEqualTo("AND");
    }

    [Test]
    public async Task ParseCondition_CaseInsensitiveKeywords_ParsesCorrectly()
    {
        var result = DynamoDbExpressionParser.ParseCondition("a = :v1 and b = :v2 or c = :v3");

        // Should parse as: (a = :v1 AND b = :v2) OR c = :v3
        await Assert.That(result).IsTypeOf<LogicalNode>();
        var or = (LogicalNode)result;
        await Assert.That(or.Operator).IsEqualTo("OR");
    }
}
