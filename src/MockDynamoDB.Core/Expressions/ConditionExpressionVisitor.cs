using Antlr4.Runtime.Tree;
using MockDynamoDB.Core.Expressions.Grammar;
using MockDynamoDB.Core.Models;

namespace MockDynamoDB.Core.Expressions;

public class ConditionExpressionVisitor : DynamoDbConditionBaseVisitor<ExpressionNode>
{
    private readonly Dictionary<string, string>? _expressionAttributeNames;

    public ConditionExpressionVisitor(Dictionary<string, string>? expressionAttributeNames)
    {
        _expressionAttributeNames = expressionAttributeNames;
    }

    public override ExpressionNode VisitCondition(DynamoDbConditionParser.ConditionContext context)
    {
        return Visit(context.orExpression());
    }

    public override ExpressionNode VisitOrExpression(DynamoDbConditionParser.OrExpressionContext context)
    {
        var expressions = context.andExpression();
        var result = Visit(expressions[0]);

        for (int i = 1; i < expressions.Length; i++)
        {
            var right = Visit(expressions[i]);
            result = new LogicalNode(result, "OR", right);
        }

        return result;
    }

    public override ExpressionNode VisitAndExpression(DynamoDbConditionParser.AndExpressionContext context)
    {
        var expressions = context.notExpression();
        var result = Visit(expressions[0]);

        for (int i = 1; i < expressions.Length; i++)
        {
            var right = Visit(expressions[i]);
            result = new LogicalNode(result, "AND", right);
        }

        return result;
    }

    public override ExpressionNode VisitNotExpression(DynamoDbConditionParser.NotExpressionContext context)
    {
        if (context.NOT() != null)
        {
            var operand = Visit(context.notExpression());
            return new NotNode(operand);
        }

        return Visit(context.comparison());
    }

    public override ExpressionNode VisitComparisonExpr(DynamoDbConditionParser.ComparisonExprContext context)
    {
        var left = VisitOperand(context.operand(0));
        var right = VisitOperand(context.operand(1));
        var op = context.comparator().GetText();
        return new ComparisonNode(left, op, right);
    }

    public override ExpressionNode VisitBetweenExpr(DynamoDbConditionParser.BetweenExprContext context)
    {
        var operands = context.operand();
        var value = VisitOperand(operands[0]);
        var low = VisitOperand(operands[1]);
        var high = VisitOperand(operands[2]);
        return new BetweenNode(value, low, high);
    }

    public override ExpressionNode VisitInExpr(DynamoDbConditionParser.InExprContext context)
    {
        var operands = context.operand();
        var value = VisitOperand(operands[0]);
        var list = new List<ExpressionNode>();
        for (int i = 1; i < operands.Length; i++)
        {
            list.Add(VisitOperand(operands[i]));
        }
        return new InNode(value, list);
    }

    public override ExpressionNode VisitFunctionExpr(DynamoDbConditionParser.FunctionExprContext context)
    {
        return VisitFunction(context.function());
    }

    public override ExpressionNode VisitParenExpr(DynamoDbConditionParser.ParenExprContext context)
    {
        return Visit(context.orExpression());
    }

    public override ExpressionNode VisitFunction(DynamoDbConditionParser.FunctionContext context)
    {
        var name = context.functionName().GetText();
        var args = new List<ExpressionNode>();
        foreach (var operand in context.operand())
        {
            args.Add(VisitOperand(operand));
        }
        return new FunctionNode(name, args);
    }

    public ExpressionNode VisitOperand(DynamoDbConditionParser.OperandContext context)
    {
        if (context.documentPath() != null)
        {
            var path = BuildDocumentPath(context.documentPath(), _expressionAttributeNames);
            return new PathNode(path);
        }

        if (context.valuePlaceholder() != null)
        {
            return new ValuePlaceholderNode(context.valuePlaceholder().PLACEHOLDER().GetText());
        }

        if (context.function() != null)
        {
            return VisitFunction(context.function());
        }

        throw new ValidationException("Unexpected operand in condition expression");
    }

    public static DocumentPath BuildDocumentPath(
        DynamoDbConditionParser.DocumentPathContext context,
        Dictionary<string, string>? expressionAttributeNames)
    {
        var elements = new List<PathElement>();

        foreach (var pathElement in context.pathElement())
        {
            if (pathElement.NAME_PLACEHOLDER() != null)
            {
                var placeholder = pathElement.NAME_PLACEHOLDER().GetText();
                var name = DocumentPath.ResolveName(placeholder, expressionAttributeNames);
                elements.Add(new AttributeElement(name));
            }
            else
            {
                elements.Add(new AttributeElement(pathElement.IDENTIFIER().GetText()));
            }
        }

        // Handle list indexes [N]
        foreach (var numberToken in context.NUMBER())
        {
            elements.Add(new IndexElement(int.Parse(numberToken.GetText())));
        }

        // The above approach is too simplistic for interleaved dots and brackets.
        // We need to walk children in order to correctly interleave attributes and indexes.
        return BuildDocumentPathFromChildren(context, expressionAttributeNames);
    }

    private static DocumentPath BuildDocumentPathFromChildren(
        DynamoDbConditionParser.DocumentPathContext context,
        Dictionary<string, string>? expressionAttributeNames)
    {
        var elements = new List<PathElement>();

        // Walk through children in order
        for (int i = 0; i < context.ChildCount; i++)
        {
            var child = context.GetChild(i);

            if (child is DynamoDbConditionParser.PathElementContext pathElem)
            {
                if (pathElem.NAME_PLACEHOLDER() != null)
                {
                    var placeholder = pathElem.NAME_PLACEHOLDER().GetText();
                    var name = DocumentPath.ResolveName(placeholder, expressionAttributeNames);
                    elements.Add(new AttributeElement(name));
                }
                else
                {
                    elements.Add(new AttributeElement(pathElem.IDENTIFIER().GetText()));
                }
            }
            else if (child is ITerminalNode terminal)
            {
                if (terminal.Symbol.Type == DynamoDbConditionParser.NUMBER)
                {
                    elements.Add(new IndexElement(int.Parse(terminal.GetText())));
                }
                // Skip DOT, LBRACKET, RBRACKET tokens
            }
        }

        return new DocumentPath(elements);
    }
}
