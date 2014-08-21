using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery
{
    public class DisregardODataInterceptor : ExpressionVisitor
    {
        // Strip out these OData query operations because the search service already did them
        private static readonly HashSet<string> methodsToIgnore = new HashSet<string>(
                new[] { "Skip", "Take", "OrderBy", "ThenBy", "OrderByDescending", "ThenByDescending", "Where" }, StringComparer.Ordinal);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;
            if ((method.DeclaringType == typeof(Queryable)) && methodsToIgnore.Contains(method.Name))
            {
                // The expression is of the format Queryable.OrderBy(<Expression>, <Order-by-params>). To avoid performing the 
                // method, we ignore it, traversing the passed in expression instead.
                return Visit(node.Arguments[0]);
            }
            return base.VisitMethodCall(node);
        }
    }
}