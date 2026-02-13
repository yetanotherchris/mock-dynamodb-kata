using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Tests.Unit;

public class UpdateExpressionParserTests
{
    [Fact]
    public void ParseUpdate_SetSimpleValue_ReturnsSetAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET #s = :val",
            new Dictionary<string, string> { { "#s", "status" } });

        var action = Assert.Single(actions);
        Assert.Equal("SET", action.Type);
        Assert.Equal("status", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.IsType<ValuePlaceholderNode>(action.Value);
    }

    [Fact]
    public void ParseUpdate_SetWithArithmetic_ReturnsArithmeticNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET count = count + :inc");

        var action = Assert.Single(actions);
        Assert.Equal("SET", action.Type);

        var arith = Assert.IsType<ArithmeticNode>(action.Value);
        Assert.Equal("+", arith.Operator);
        Assert.IsType<PathNode>(arith.Left);
        Assert.IsType<ValuePlaceholderNode>(arith.Right);
    }

    [Fact]
    public void ParseUpdate_SetWithSubtraction_ReturnsArithmeticNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET stock = stock - :dec");

        var action = Assert.Single(actions);
        var arith = Assert.IsType<ArithmeticNode>(action.Value);
        Assert.Equal("-", arith.Operator);
    }

    [Fact]
    public void ParseUpdate_SetWithIfNotExists_ReturnsFunctionNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET val = if_not_exists(val, :default)");

        var action = Assert.Single(actions);
        var func = Assert.IsType<FunctionNode>(action.Value);
        Assert.Equal("if_not_exists", func.FunctionName);
        Assert.Equal(2, func.Arguments.Count);
    }

    [Fact]
    public void ParseUpdate_SetWithListAppend_ReturnsFunctionNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET items = list_append(items, :newItems)");

        var action = Assert.Single(actions);
        var func = Assert.IsType<FunctionNode>(action.Value);
        Assert.Equal("list_append", func.FunctionName);
        Assert.Equal(2, func.Arguments.Count);
    }

    [Fact]
    public void ParseUpdate_RemoveSinglePath_ReturnsRemoveAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("REMOVE attr1");

        var action = Assert.Single(actions);
        Assert.Equal("REMOVE", action.Type);
        Assert.Equal("attr1", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.Null(action.Value);
    }

    [Fact]
    public void ParseUpdate_RemoveMultiplePaths_ReturnsMultipleActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("REMOVE attr1, attr2.nested");

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal("REMOVE", a.Type));

        Assert.Equal("attr1", ((AttributeElement)actions[0].Path.Elements[0]).Name);
        Assert.Equal("attr2", ((AttributeElement)actions[1].Path.Elements[0]).Name);
        Assert.Equal("nested", ((AttributeElement)actions[1].Path.Elements[1]).Name);
    }

    [Fact]
    public void ParseUpdate_AddAction_ReturnsAddAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("ADD counter :inc");

        var action = Assert.Single(actions);
        Assert.Equal("ADD", action.Type);
        Assert.Equal("counter", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.IsType<ValuePlaceholderNode>(action.Value);
    }

    [Fact]
    public void ParseUpdate_DeleteAction_ReturnsDeleteAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("DELETE tags :removals");

        var action = Assert.Single(actions);
        Assert.Equal("DELETE", action.Type);
        Assert.Equal("tags", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.IsType<ValuePlaceholderNode>(action.Value);
    }

    [Fact]
    public void ParseUpdate_CombinedClauses_ReturnsAllActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET a = :v1 REMOVE b ADD c :v2");

        Assert.Equal(3, actions.Count);
        Assert.Equal("SET", actions[0].Type);
        Assert.Equal("REMOVE", actions[1].Type);
        Assert.Equal("ADD", actions[2].Type);
    }

    [Fact]
    public void ParseUpdate_AddAndDeleteCombined_ReturnsAllActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("ADD counter :inc DELETE tags :removals");

        Assert.Equal(2, actions.Count);
        Assert.Equal("ADD", actions[0].Type);
        Assert.Equal("DELETE", actions[1].Type);
    }

    [Fact]
    public void ParseUpdate_MultipleSetActions_ReturnsAll()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET a = :v1, b = :v2, c = :v3");

        Assert.Equal(3, actions.Count);
        Assert.All(actions, a => Assert.Equal("SET", a.Type));
    }

    [Fact]
    public void ParseUpdate_SetWithDocumentPath_ResolvesPath()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET user.name = :name");

        var action = Assert.Single(actions);
        Assert.Equal(2, action.Path.Elements.Count);
        Assert.Equal("user", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.Equal("name", ((AttributeElement)action.Path.Elements[1]).Name);
    }

    [Fact]
    public void ParseUpdate_SetWithListIndex_ResolvesPath()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET items[0].name = :name");

        var action = Assert.Single(actions);
        Assert.Equal(3, action.Path.Elements.Count);
        Assert.Equal("items", ((AttributeElement)action.Path.Elements[0]).Name);
        Assert.Equal(0, ((IndexElement)action.Path.Elements[1]).Index);
        Assert.Equal("name", ((AttributeElement)action.Path.Elements[2]).Name);
    }

    [Fact]
    public void ParseUpdate_ExpressionAttributeNames_ResolvesPlaceholders()
    {
        var names = new Dictionary<string, string> { { "#s", "status" }, { "#n", "name" } };

        var actions = DynamoDbExpressionParser.ParseUpdate("SET #s = :val, #n = :name", names);

        Assert.Equal(2, actions.Count);
        Assert.Equal("status", ((AttributeElement)actions[0].Path.Elements[0]).Name);
        Assert.Equal("name", ((AttributeElement)actions[1].Path.Elements[0]).Name);
    }

    [Fact]
    public void ParseUpdate_MalformedExpression_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            DynamoDbExpressionParser.ParseUpdate("INVALID expression"));
    }

    [Fact]
    public void ParseUpdate_SetWithPathAsValue_ReturnsPathNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET backup = original");

        var action = Assert.Single(actions);
        var pathValue = Assert.IsType<PathNode>(action.Value);
        Assert.Equal("original", ((AttributeElement)pathValue.Path.Elements[0]).Name);
    }
}
