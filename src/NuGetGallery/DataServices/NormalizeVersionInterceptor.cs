using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NuGetGallery
{
    public class NormalizeVersionInterceptor : ExpressionVisitor
    {
        private static MemberInfo _versionMember = typeof(V2FeedPackage).GetProperty("Version");
        private static MemberInfo _normalizedVersionMember = typeof(V2FeedPackage).GetProperty("NormalizedVersion");
        
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Change equality comparisons on Version to normalized comparisons on NormalizedVersion
            if (node.NodeType == ExpressionType.Equal)
            {
                // Figure out which side is the target
                ConstantExpression constSide = (node.Left as ConstantExpression) ?? (node.Right as ConstantExpression);
                if (constSide != null && constSide.Type == typeof(string))
                {
                    MemberExpression memberSide = (node.Right as MemberExpression) ?? (node.Left as MemberExpression);
                    if (memberSide != null && memberSide.Member == _versionMember)
                    {
                        // We have a "Package.Version == <constant>" expression!
                        
                        // Transform the constant version into a normalized version
                        string newVersion = SemanticVersionExtensions.Normalize((string)constSide.Value);

                        // Create a new expression that checks the new constant against NormalizedVersion instead
                        return Expression.MakeBinary(
                            ExpressionType.Equal,
                            left: Expression.Constant(newVersion),
                            right: Expression.MakeMemberAccess(memberSide.Expression, _normalizedVersionMember));
                    }
                }
            }
            return node;
        }
    }
}
