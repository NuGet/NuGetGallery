using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery
{
    public static class Extensions
    {
        // Search criteria
        private static Func<string, Expression<Func<Package, bool>>> idCriteria = term =>
            p => p.PackageRegistration.Id.Contains(term);

        private static Func<string, Expression<Func<Package, bool>>> descriptionCriteria = term =>
            p => p.Description.Contains(term);

        private static Func<string, Expression<Func<Package, bool>>> summaryCriteria = term =>
            p => p.Summary.Contains(term);

        private static Func<string, Expression<Func<Package, bool>>> tagCriteria = term =>
            p => p.Tags.Contains(term);

        private static Func<string, Expression<Func<Package, bool>>> authorCriteria = term =>
            p => p.Authors.Any(a => a.Name.Contains(term));

        private static Func<string, Expression<Func<Package, bool>>>[] searchCriteria = new[] { 
                idCriteria, 
                authorCriteria,
                descriptionCriteria, 
                summaryCriteria, 
                tagCriteria 
        };

        public static IQueryable<Package> Search(this IQueryable<Package> source, string searchTerm)
        {
            if (String.IsNullOrWhiteSpace(searchTerm))
            {
                return source;
            }

            // Split the search terms by spaces
            var terms = (searchTerm ?? String.Empty).Split();

            // Build a list of expressions for each term
            var expressions = new List<LambdaExpression>();
            foreach (var criteria in searchCriteria)
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
            var parameterExpr = Expression.Parameter(typeof(Package));

            // Fix up the body to use our parameter expression
            body = new ParameterExpressionReplacer(parameterExpr).Visit(body);

            // Build the final predicate
            var predicate = Expression.Lambda<Func<Package, bool>>(body, parameterExpr);

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