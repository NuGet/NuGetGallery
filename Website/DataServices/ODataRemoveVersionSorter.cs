using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

namespace NuGetGallery
{
    public class ODataRemoveVersionSorter : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsSortingOnVersion(node))
            {
                // The expression is of the format Queryable.OrderBy(<Expression>, <Order-by-params>). To avoid performing the 
                // method, we ignore it, traversing the passed in expression instead.
                return Visit(node.Arguments[0]);
            }
            return base.VisitMethodCall(node);
        }

        private bool IsSortingOnVersion(MethodCallExpression expression)
        {
            var methodsToIgnore = new[] { "ThenBy", "ThenByDescending" };
            var method = expression.Method;

            return method.DeclaringType == typeof(Queryable) &&
                   methodsToIgnore.Contains(method.Name, StringComparer.Ordinal) &&
                   IsVersionArgument(expression);
        }

        private bool IsVersionArgument(MethodCallExpression expression)
        {
            if (expression.Arguments.Count == 2 && expression.Arguments[1].NodeType == ExpressionType.Quote)
            {
                var unaryExpression = expression.Arguments[1] as UnaryExpression;
                if (unaryExpression != null)
                {
                    var lambdaExpression = unaryExpression.Operand as LambdaExpression;
                    if (lambdaExpression != null)
                    {
                        var memberAccess = lambdaExpression.Body as MemberExpression;
                        return memberAccess != null && memberAccess.Member.Name.Equals("Version", StringComparison.Ordinal);
                    }
                }
            }
            return false;
        }
    }
}