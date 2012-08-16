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
                // The expression is of the format Queryable.ThenBy(OrderBy(<Expression>, <Order-by-params>), <Then-by-params>). To avoid performing the 
                // method, we ignore it, traversing the passed in expression instead.
                return Visit(node.Arguments[0]);
            }
            return base.VisitMethodCall(node);
        }

        private bool IsSortingOnVersion(MethodCallExpression expression)
        {
            var methodsToIgnore = new[] { "ThenBy", "ThenByDescending" };
            var method = expression.Method;

            if (method.DeclaringType == typeof(Queryable) && methodsToIgnore.Contains(method.Name, StringComparer.Ordinal))
            {
                return IsVersionArgument(expression);
            }

            return false;
        }

        private bool IsVersionArgument(MethodCallExpression expression)
        {
            if (expression.Arguments.Count == 2)
            {
                var memberVisitor = new MemberVisitor();
                memberVisitor.Visit(expression.Arguments[1]);
                return memberVisitor.Flag;
            }

            return false;
        }

        private sealed class MemberVisitor : ExpressionVisitor
        {
            public bool Flag { get; set; }

            protected override Expression VisitMember(MemberExpression node)
            {
                // Note that if Flag has already been set to true, we need to retain that state
                // as our visitor can be called multiple times.
                // Example: The expression can either be p => p.Version or p => p.ExpandedWrapper.Version where the 
                // latter is some funky OData type wrapper. We need to ensure we handle both these cases
                Flag = Flag || String.Equals(node.Member.Name, "Version", StringComparison.Ordinal);
                return base.VisitMember(node);
            }
        }

    }
}