using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NuGetGallery.DataServices
{
    public class LastUpdatedHijacker : IQueryable<Package>
    {
        private OrderbyHijackerProvider _provider;
        public IQueryable<Package> Inner { get; private set; }

        public LastUpdatedHijacker(IQueryable<Package> inner)
        {
            Inner = inner;
            _provider = new OrderbyHijackerProvider(inner.Provider);
        }

        public IEnumerator<Package> GetEnumerator()
        {
            return Inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Inner.GetEnumerator();
        }

        public Type ElementType
        {
            get { return Inner.ElementType; }
        }

        public Expression Expression
        {
            get { return Inner.Expression; }
        }

        public IQueryProvider Provider
        {
            get { return _provider; }
        }

        private class OrderbyHijackerProvider : IQueryProvider
        {
            public IQueryProvider Inner { get; private set; }

            public OrderbyHijackerProvider(IQueryProvider inner)
            {
                Inner = inner;
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return Inner.CreateQuery<TElement>(expression);
            }

            public IQueryable CreateQuery(Expression expression)
            {
                Expression remnant = expression;
                MethodCallExpression orderBy;
                while (IsQueryableOrderby(orderBy = remnant as MethodCallExpression))
                {
                    remnant = orderBy.Arguments[0];
                }

                LastUpdatedModifier modifier = new LastUpdatedModifier();
                Expression modifiedExpression = modifier.Modify(expression);

                return Inner.CreateQuery(expression);
            }

            public TResult Execute<TResult>(Expression expression)
            {
                return Inner.Execute<TResult>(expression);
            }

            public object Execute(Expression expression)
            {
                return Inner.Execute(expression);
            }

            private static bool IsQueryableOrderby(MethodCallExpression expr)
            {
                return expr != null &&
                    expr.Method.DeclaringType == typeof(Queryable) &&
                    String.Equals(expr.Method.Name, "OrderBy", StringComparison.Ordinal);
            }

            class LastUpdatedModifier : ExpressionVisitor
            {
                public Expression Modify(Expression expression)
                {
                    return Visit(expression);
                }

                protected override Expression VisitBinary(BinaryExpression node)
                {
                    if (node.NodeType == ExpressionType.GreaterThan)
                    {
                        if (node.Left.NodeType == ExpressionType.MemberAccess)
                        {
                            MemberExpression left = (MemberExpression)node.Left;

                            string name = left.Member.Name;

                            if (name == "LastUpdated")
                            {
                                Type type = typeof(NuGetGallery.V2FeedPackage);
                                MemberInfo created = type.GetMember("Created")[0];
                                MemberExpression newLeft = Expression.MakeMemberAccess(left.Expression, created);

                                return node.Update(newLeft, node.Conversion, node.Right);
                            }
                        }
                    }

                    return base.VisitBinary(node);
                }
            }
        }
    }

    public static class LastUpdatedHijackerExtensions
    {
        public static IQueryable<Package> UseLastUpdatedRewrite(this IQueryable<Package> self)
        {
            return new LastUpdatedHijacker(self);
        }
    }
}