using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

//  this is a generic intercept on the query expression and can be used for debugging and diagnostics

namespace NuGetGallery.DataServices
{
    public class QueryInterceptor : IQueryable<Package>
    {
        private QueryInterceptorProvider _provider;
        public IQueryable<Package> Inner { get; private set; }

        public QueryInterceptor(IQueryable<Package> inner)
        {
            Inner = inner;
            _provider = new QueryInterceptorProvider(inner.Provider);
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

        private class QueryInterceptorProvider : IQueryProvider
        {
            public IQueryProvider Inner { get; private set; }

            public QueryInterceptorProvider(IQueryProvider inner)
            {
                Inner = inner;
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return Inner.CreateQuery<TElement>(expression);
            }

            public IQueryable CreateQuery(Expression expression)
            {
                QueryInterceptorVisitor visitor = new QueryInterceptorVisitor();
                visitor.Visit(expression);

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

            class QueryInterceptorVisitor : ExpressionVisitor
            {
                protected override Expression VisitBinary(BinaryExpression node)
                {
                    return base.VisitBinary(node);
                }

                protected override Expression VisitBlock(BlockExpression node)
                {
                    return base.VisitBlock(node);
                }

                protected override CatchBlock VisitCatchBlock(CatchBlock node)
                {
                    return base.VisitCatchBlock(node);
                }

                protected override Expression VisitConditional(ConditionalExpression node)
                {
                    return base.VisitConditional(node);
                }

                protected override Expression VisitConstant(ConstantExpression node)
                {
                    return base.VisitConstant(node);
                }

                protected override Expression VisitDebugInfo(DebugInfoExpression node)
                {
                    return base.VisitDebugInfo(node);
                }

                protected override Expression VisitDefault(DefaultExpression node)
                {
                    return base.VisitDefault(node);
                }

                protected override Expression VisitDynamic(DynamicExpression node)
                {
                    return base.VisitDynamic(node);
                }

                protected override ElementInit VisitElementInit(ElementInit node)
                {
                    return base.VisitElementInit(node);
                }

                protected override Expression VisitExtension(Expression node)
                {
                    return base.VisitExtension(node);
                }

                protected override Expression VisitGoto(GotoExpression node)
                {
                    return base.VisitGoto(node);
                }

                protected override Expression VisitIndex(IndexExpression node)
                {
                    return base.VisitIndex(node);
                }

                protected override Expression VisitInvocation(InvocationExpression node)
                {
                    return base.VisitInvocation(node);
                }

                protected override Expression VisitLabel(LabelExpression node)
                {
                    return base.VisitLabel(node);
                }

                protected override LabelTarget VisitLabelTarget(LabelTarget node)
                {
                    return base.VisitLabelTarget(node);
                }

                protected override Expression VisitLambda<T>(Expression<T> node)
                {
                    return base.VisitLambda<T>(node);
                }

                protected override Expression VisitListInit(ListInitExpression node)
                {
                    return base.VisitListInit(node);
                }

                protected override Expression VisitLoop(LoopExpression node)
                {
                    return base.VisitLoop(node);
                }

                protected override Expression VisitMember(MemberExpression node)
                {
                    return base.VisitMember(node);
                }

                protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
                {
                    return base.VisitMemberAssignment(node);
                }

                protected override MemberBinding VisitMemberBinding(MemberBinding node)
                {
                    return base.VisitMemberBinding(node);
                }

                protected override Expression VisitMemberInit(MemberInitExpression node)
                {
                    return base.VisitMemberInit(node);
                }

                protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
                {
                    return base.VisitMemberListBinding(node);
                }

                protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
                {
                    return base.VisitMemberMemberBinding(node);
                }

                protected override Expression VisitMethodCall(MethodCallExpression node)
                {
                    return base.VisitMethodCall(node);
                }

                protected override Expression VisitNew(NewExpression node)
                {
                    return base.VisitNew(node);
                }

                protected override Expression VisitNewArray(NewArrayExpression node)
                {
                    return base.VisitNewArray(node);
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    return base.VisitParameter(node);
                }

                protected override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
                {
                    return base.VisitRuntimeVariables(node);
                }

                protected override Expression VisitSwitch(SwitchExpression node)
                {
                    return base.VisitSwitch(node);
                }

                protected override SwitchCase VisitSwitchCase(SwitchCase node)
                {
                    return base.VisitSwitchCase(node);
                }

                protected override Expression VisitTry(TryExpression node)
                {
                    return base.VisitTry(node);
                }

                protected override Expression VisitTypeBinary(TypeBinaryExpression node)
                {
                    return base.VisitTypeBinary(node);
                }

                protected override Expression VisitUnary(UnaryExpression node)
                {
                    return base.VisitUnary(node);
                }
            }
        }
    }

    public static class QueryInterceptorExtensions
    {
        public static IQueryable<Package> UseQueryInterceptor(this IQueryable<Package> self)
        {
            return new QueryInterceptor(self);
        }
    }
}