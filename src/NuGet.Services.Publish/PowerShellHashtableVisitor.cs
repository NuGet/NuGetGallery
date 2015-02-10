using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Web;

namespace NuGet.Services.Publish
{
    internal class PowerShellHashtableVisitor : ICustomAstVisitor
    {
        public static Hashtable GetHashtable(HashtableAst ast)
        {
            if (ast != null)
            {
                return ast.Visit(new PowerShellHashtableVisitor()) as Hashtable;
            }

            return null;
        }

        public object VisitErrorStatement(ErrorStatementAst errorStatementAst) { throw new UnexpectedElementException(); }
        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst) { throw new UnexpectedElementException(); }
        public object VisitScriptBlock(ScriptBlockAst scriptBlockAst) { throw new UnexpectedElementException(); }
        public object VisitParamBlock(ParamBlockAst paramBlockAst) { throw new UnexpectedElementException(); }
        public object VisitNamedBlock(NamedBlockAst namedBlockAst) { throw new UnexpectedElementException(); }
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) { throw new UnexpectedElementException(); }
        public object VisitAttribute(AttributeAst attributeAst) { throw new UnexpectedElementException(); }
        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst) { throw new UnexpectedElementException(); }
        public object VisitParameter(ParameterAst parameterAst) { throw new UnexpectedElementException(); }
        public object VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst) { throw new UnexpectedElementException(); }
        public object VisitIfStatement(IfStatementAst ifStmtAst) { throw new UnexpectedElementException(); }
        public object VisitTrap(TrapStatementAst trapStatementAst) { throw new UnexpectedElementException(); }
        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst) { throw new UnexpectedElementException(); }
        public object VisitDataStatement(DataStatementAst dataStatementAst) { throw new UnexpectedElementException(); }
        public object VisitForEachStatement(ForEachStatementAst forEachStatementAst) { throw new UnexpectedElementException(); }
        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst) { throw new UnexpectedElementException(); }
        public object VisitForStatement(ForStatementAst forStatementAst) { throw new UnexpectedElementException(); }
        public object VisitWhileStatement(WhileStatementAst whileStatementAst) { throw new UnexpectedElementException(); }
        public object VisitCatchClause(CatchClauseAst catchClauseAst) { throw new UnexpectedElementException(); }
        public object VisitTryStatement(TryStatementAst tryStatementAst) { throw new UnexpectedElementException(); }
        public object VisitBreakStatement(BreakStatementAst breakStatementAst) { throw new UnexpectedElementException(); }
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) { throw new UnexpectedElementException(); }
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) { throw new UnexpectedElementException(); }
        public object VisitExitStatement(ExitStatementAst exitStatementAst) { throw new UnexpectedElementException(); }
        public object VisitThrowStatement(ThrowStatementAst throwStatementAst) { throw new UnexpectedElementException(); }
        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst) { throw new UnexpectedElementException(); }
        public object VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst) { throw new UnexpectedElementException(); }
        public object VisitCommand(CommandAst commandAst) { throw new UnexpectedElementException(); }
        public object VisitCommandExpression(CommandExpressionAst commandExpressionAst) { throw new UnexpectedElementException(); }
        public object VisitCommandParameter(CommandParameterAst commandParameterAst) { throw new UnexpectedElementException(); }
        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst) { throw new UnexpectedElementException(); }
        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst) { throw new UnexpectedElementException(); }
        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst) { throw new UnexpectedElementException(); }
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) { throw new UnexpectedElementException(); }
        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst) { throw new UnexpectedElementException(); }
        public object VisitBlockStatement(BlockStatementAst blockStatementAst) { throw new UnexpectedElementException(); }
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) { throw new UnexpectedElementException(); }

        public object VisitStatementBlock(StatementBlockAst statementBlockAst)
        {
            if (statementBlockAst != null)
            {
                var statements = statementBlockAst.Statements;
                if (statements != null)
                {
                    var firstStatement = statements.FirstOrDefault();
                    if (firstStatement != null)
                    {
                        return firstStatement.Visit(this);
                    }
                }
            }

            return null;
        }

        public object VisitPipeline(PipelineAst pipelineAst)
        {
            var expr = pipelineAst.GetPureExpression();
            if (expr != null)
            {
                return expr.Visit(this);
            }
            throw new UnexpectedElementException();
        }

        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst)
        {
            return constantExpressionAst.Value;
        }

        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst)
        {
            return stringConstantExpressionAst.Value;
        }

        public object VisitSubExpression(SubExpressionAst subExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst)
        {
            return arrayExpressionAst.SubExpression.Visit(this);
        }

        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst)
        {
            return arrayLiteralAst.Elements.Select(e => e.Visit(this)).ToArray();
        }

        public object VisitScriptBlockExpression(ScriptBlockExpressionAst scriptBlockExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitParenExpression(ParenExpressionAst parenExpressionAst)
        {
            throw new UnexpectedElementException();
        }

        public object VisitHashtable(HashtableAst hashtableAst)
        {
            var result = new Hashtable(StringComparer.InvariantCultureIgnoreCase);
            foreach (var pair in hashtableAst.KeyValuePairs)
            {
                result.Add(pair.Item1.Visit(this), pair.Item2.Visit(this));
            }
            return result;
        }
    }

    public class UnexpectedElementException : Exception
    {
        public UnexpectedElementException() : base("Unexpected element encountered.")
        {
        }
    }
}