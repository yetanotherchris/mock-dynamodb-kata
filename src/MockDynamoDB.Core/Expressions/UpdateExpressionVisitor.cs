using Antlr4.Runtime.Tree;
using MockDynamoDB.Core.Expressions.Grammar;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class UpdateExpressionVisitor : DynamoDbUpdateBaseVisitor<object>
{
    private readonly Dictionary<string, string>? _expressionAttributeNames;
    private readonly List<UpdateAction> _actions = [];

    public UpdateExpressionVisitor(Dictionary<string, string>? expressionAttributeNames)
    {
        _expressionAttributeNames = expressionAttributeNames;
    }

    public List<UpdateAction> GetActions() => _actions;

    public override object VisitUpdateExpression(DynamoDbUpdateParser.UpdateExpressionContext context)
    {
        foreach (var clause in context.clause())
        {
            Visit(clause);
        }
        return _actions;
    }

    public override object VisitSetClause(DynamoDbUpdateParser.SetClauseContext context)
    {
        foreach (var action in context.setAction())
        {
            Visit(action);
        }
        return _actions;
    }

    public override object VisitSetAction(DynamoDbUpdateParser.SetActionContext context)
    {
        var path = BuildDocumentPath(context.documentPath());
        var value = VisitSetValue(context.setValue());
        _actions.Add(new UpdateAction { Type = "SET", Path = path, Value = value });
        return _actions;
    }

    private ExpressionNode VisitSetValue(DynamoDbUpdateParser.SetValueContext context)
    {
        if (context is DynamoDbUpdateParser.ArithmeticValueContext arith)
        {
            var left = VisitOperand(arith.operand(0));
            var right = VisitOperand(arith.operand(1));
            var op = arith.PLUS() != null ? "+" : "-";
            return new ArithmeticNode(left, op, right);
        }

        if (context is DynamoDbUpdateParser.SingleValueContext single)
        {
            return VisitOperand(single.operand());
        }

        throw new ValidationException("Unexpected SET value in update expression");
    }

    public override object VisitRemoveClause(DynamoDbUpdateParser.RemoveClauseContext context)
    {
        foreach (var pathCtx in context.documentPath())
        {
            var path = BuildDocumentPath(pathCtx);
            _actions.Add(new UpdateAction { Type = "REMOVE", Path = path });
        }
        return _actions;
    }

    public override object VisitAddClause(DynamoDbUpdateParser.AddClauseContext context)
    {
        foreach (var action in context.addAction())
        {
            Visit(action);
        }
        return _actions;
    }

    public override object VisitAddAction(DynamoDbUpdateParser.AddActionContext context)
    {
        var path = BuildDocumentPath(context.documentPath());
        var value = VisitOperand(context.operand());
        _actions.Add(new UpdateAction { Type = "ADD", Path = path, Value = value });
        return _actions;
    }

    public override object VisitDeleteClause(DynamoDbUpdateParser.DeleteClauseContext context)
    {
        foreach (var action in context.deleteAction())
        {
            Visit(action);
        }
        return _actions;
    }

    public override object VisitDeleteAction(DynamoDbUpdateParser.DeleteActionContext context)
    {
        var path = BuildDocumentPath(context.documentPath());
        var value = VisitOperand(context.operand());
        _actions.Add(new UpdateAction { Type = "DELETE", Path = path, Value = value });
        return _actions;
    }

    private ExpressionNode VisitOperand(DynamoDbUpdateParser.OperandContext context)
    {
        if (context.documentPath() != null)
        {
            var path = BuildDocumentPath(context.documentPath());
            return new PathNode(path);
        }

        if (context.valuePlaceholder() != null)
        {
            return new ValuePlaceholderNode(context.valuePlaceholder().PLACEHOLDER().GetText());
        }

        if (context.function() != null)
        {
            return VisitUpdateFunction(context.function());
        }

        throw new ValidationException("Unexpected operand in update expression");
    }

    private ExpressionNode VisitUpdateFunction(DynamoDbUpdateParser.FunctionContext context)
    {
        var name = context.functionName().GetText();
        var args = new List<ExpressionNode>();
        foreach (var operand in context.operand())
        {
            args.Add(VisitOperand(operand));
        }
        return new FunctionNode(name, args);
    }

    private DocumentPath BuildDocumentPath(DynamoDbUpdateParser.DocumentPathContext context)
    {
        var elements = new List<PathElement>();

        for (int i = 0; i < context.ChildCount; i++)
        {
            var child = context.GetChild(i);

            if (child is DynamoDbUpdateParser.PathElementContext pathElem)
            {
                if (pathElem.NAME_PLACEHOLDER() != null)
                {
                    var placeholder = pathElem.NAME_PLACEHOLDER().GetText();
                    var name = DocumentPath.ResolveName(placeholder, _expressionAttributeNames);
                    elements.Add(new AttributeElement(name));
                }
                else
                {
                    elements.Add(new AttributeElement(pathElem.IDENTIFIER().GetText()));
                }
            }
            else if (child is ITerminalNode terminal)
            {
                if (terminal.Symbol.Type == DynamoDbUpdateParser.NUMBER)
                {
                    elements.Add(new IndexElement(int.Parse(terminal.GetText())));
                }
            }
        }

        return new DocumentPath(elements);
    }
}
