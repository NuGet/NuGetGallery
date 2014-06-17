using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery
{
    public static class ManagedFeedExtensions
    {
        // Search criteria
        private static readonly Func<string, Expression<Func<FeedPackage, bool>>> IdCriteria = term =>
                                                                                           fp => fp.Package.PackageRegistration.Id.Contains(term);

        private static readonly Func<string, Expression<Func<FeedPackage, bool>>> DescriptionCriteria = term =>
                                                                                                    fp => fp.Package.Description.Contains(term);

        private static readonly Func<string, Expression<Func<FeedPackage, bool>>> SummaryCriteria = term =>
                                                                                                fp => fp.Package.Summary.Contains(term);

        private static readonly Func<string, Expression<Func<FeedPackage, bool>>> TagCriteria = term =>
                                                                                            fp => fp.Package.Tags.Contains(term);

        private static readonly Func<string, Expression<Func<FeedPackage, bool>>> AuthorCriteria = term =>
                                                                                               fp => fp.Package.FlattenedAuthors.Contains(term);

        private static readonly Func<string, Expression<Func<FeedPackage, bool>>>[] SearchCriteria = new[]
            {
                IdCriteria,
                AuthorCriteria,
                DescriptionCriteria,
                SummaryCriteria,
                TagCriteria
            };

        public static IQueryable<FeedPackage> Search(this IQueryable<FeedPackage> source, string searchTerm)
        {
            if (String.IsNullOrWhiteSpace(searchTerm))
            {
                return source;
            }

            // Split the search terms by spaces
            var terms = searchTerm.Split();

            // Build a list of expressions for each term
            var expressions = new List<LambdaExpression>();
            foreach (var criteria in SearchCriteria)
            {
                foreach (var term in terms)
                {
                    expressions.Add(criteria(term));
                }
            }

            // Build a giant or statement using the bodies of the lambdas
            var body = expressions.Select(p => p.Body)
                .Aggregate(Expression.OrElse);

            // Now build the final predicate
            var parameterExpr = Expression.Parameter(typeof(FeedPackage));

            // Fix up the body to use our parameter expression
            body = new ParameterExpressionReplacer(parameterExpr).Visit(body);

            // Build the final predicate
            var predicate = Expression.Lambda<Func<FeedPackage, bool>>(body, parameterExpr);

            // Apply it to the query
            return source.Where(predicate);
        }

        private class ParameterExpressionReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _parameterExpr;

            public ParameterExpressionReplacer(ParameterExpression parameterExpr)
            {
                _parameterExpr = parameterExpr;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Type == _parameterExpr.Type &&
                    node != _parameterExpr)
                {
                    return _parameterExpr;
                }
                return base.VisitParameter(node);
            }
        }
    }
}