using MockDynamoDB.Core.Expressions;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Tests.Unit;

public class UpdateExpressionParserTests
{
    [Test]
    public async Task ParseUpdate_SetSimpleValue_ReturnsSetAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET #s = :val",
            new Dictionary<string, string> { { "#s", "status" } });

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Type).IsEqualTo("SET");
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("status");
        await Assert.That(action.Value).IsTypeOf<ValuePlaceholderNode>();
    }

    [Test]
    public async Task ParseUpdate_SetWithArithmetic_ReturnsArithmeticNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET count = count + :inc");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Type).IsEqualTo("SET");

        await Assert.That(action.Value).IsTypeOf<ArithmeticNode>();
        var arith = (ArithmeticNode)action.Value!;
        await Assert.That(arith.Operator).IsEqualTo("+");
        await Assert.That(arith.Left).IsTypeOf<PathNode>();
        await Assert.That(arith.Right).IsTypeOf<ValuePlaceholderNode>();
    }

    [Test]
    public async Task ParseUpdate_SetWithSubtraction_ReturnsArithmeticNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET stock = stock - :dec");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Value).IsTypeOf<ArithmeticNode>();
        var arith = (ArithmeticNode)action.Value!;
        await Assert.That(arith.Operator).IsEqualTo("-");
    }

    [Test]
    public async Task ParseUpdate_SetWithIfNotExists_ReturnsFunctionNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET val = if_not_exists(val, :default)");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Value).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)action.Value!;
        await Assert.That(func.FunctionName).IsEqualTo("if_not_exists");
        await Assert.That(func.Arguments).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseUpdate_SetWithListAppend_ReturnsFunctionNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET items = list_append(items, :newItems)");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Value).IsTypeOf<FunctionNode>();
        var func = (FunctionNode)action.Value!;
        await Assert.That(func.FunctionName).IsEqualTo("list_append");
        await Assert.That(func.Arguments).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseUpdate_RemoveSinglePath_ReturnsRemoveAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("REMOVE attr1");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Type).IsEqualTo("REMOVE");
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("attr1");
        await Assert.That(action.Value).IsNull();
    }

    [Test]
    public async Task ParseUpdate_RemoveMultiplePaths_ReturnsMultipleActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("REMOVE attr1, attr2.nested");

        await Assert.That(actions).Count().IsEqualTo(2);
        foreach (var a in actions)
        {
            await Assert.That(a.Type).IsEqualTo("REMOVE");
        }

        await Assert.That(((AttributeElement)actions[0].Path.Elements[0]).Name).IsEqualTo("attr1");
        await Assert.That(((AttributeElement)actions[1].Path.Elements[0]).Name).IsEqualTo("attr2");
        await Assert.That(((AttributeElement)actions[1].Path.Elements[1]).Name).IsEqualTo("nested");
    }

    [Test]
    public async Task ParseUpdate_AddAction_ReturnsAddAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("ADD counter :inc");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Type).IsEqualTo("ADD");
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("counter");
        await Assert.That(action.Value).IsTypeOf<ValuePlaceholderNode>();
    }

    [Test]
    public async Task ParseUpdate_DeleteAction_ReturnsDeleteAction()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("DELETE tags :removals");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Type).IsEqualTo("DELETE");
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("tags");
        await Assert.That(action.Value).IsTypeOf<ValuePlaceholderNode>();
    }

    [Test]
    public async Task ParseUpdate_CombinedClauses_ReturnsAllActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET a = :v1 REMOVE b ADD c :v2");

        await Assert.That(actions).Count().IsEqualTo(3);
        await Assert.That(actions[0].Type).IsEqualTo("SET");
        await Assert.That(actions[1].Type).IsEqualTo("REMOVE");
        await Assert.That(actions[2].Type).IsEqualTo("ADD");
    }

    [Test]
    public async Task ParseUpdate_AddAndDeleteCombined_ReturnsAllActions()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("ADD counter :inc DELETE tags :removals");

        await Assert.That(actions).Count().IsEqualTo(2);
        await Assert.That(actions[0].Type).IsEqualTo("ADD");
        await Assert.That(actions[1].Type).IsEqualTo("DELETE");
    }

    [Test]
    public async Task ParseUpdate_MultipleSetActions_ReturnsAll()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET a = :v1, b = :v2, c = :v3");

        await Assert.That(actions).Count().IsEqualTo(3);
        foreach (var a in actions)
        {
            await Assert.That(a.Type).IsEqualTo("SET");
        }
    }

    [Test]
    public async Task ParseUpdate_SetWithDocumentPath_ResolvesPath()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET user.name = :name");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Path.Elements).Count().IsEqualTo(2);
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("user");
        await Assert.That(((AttributeElement)action.Path.Elements[1]).Name).IsEqualTo("name");
    }

    [Test]
    public async Task ParseUpdate_SetWithListIndex_ResolvesPath()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET items[0].name = :name");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Path.Elements).Count().IsEqualTo(3);
        await Assert.That(((AttributeElement)action.Path.Elements[0]).Name).IsEqualTo("items");
        await Assert.That(((IndexElement)action.Path.Elements[1]).Index).IsEqualTo(0);
        await Assert.That(((AttributeElement)action.Path.Elements[2]).Name).IsEqualTo("name");
    }

    [Test]
    public async Task ParseUpdate_ExpressionAttributeNames_ResolvesPlaceholders()
    {
        var names = new Dictionary<string, string> { { "#s", "status" }, { "#n", "name" } };

        var actions = DynamoDbExpressionParser.ParseUpdate("SET #s = :val, #n = :name", names);

        await Assert.That(actions).Count().IsEqualTo(2);
        await Assert.That(((AttributeElement)actions[0].Path.Elements[0]).Name).IsEqualTo("status");
        await Assert.That(((AttributeElement)actions[1].Path.Elements[0]).Name).IsEqualTo("name");
    }

    [Test]
    public async Task ParseUpdate_MalformedExpression_ThrowsValidationException()
    {
        await Assert.That(() =>
            DynamoDbExpressionParser.ParseUpdate("INVALID expression"))
            .ThrowsExactly<ValidationException>();
    }

    [Test]
    public async Task ParseUpdate_SetWithPathAsValue_ReturnsPathNode()
    {
        var actions = DynamoDbExpressionParser.ParseUpdate("SET backup = original");

        await Assert.That(actions).Count().IsEqualTo(1);
        var action = actions[0];
        await Assert.That(action.Value).IsTypeOf<PathNode>();
        var pathValue = (PathNode)action.Value!;
        await Assert.That(((AttributeElement)pathValue.Path.Elements[0]).Name).IsEqualTo("original");
    }
}
