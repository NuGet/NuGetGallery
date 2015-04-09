using System;
using System.Collections.Generic;
using System.Data.Objects;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using NuGet.Services.Search.Models;

namespace NuGetGallery.DataServices
{
    public class SearchHijacker : IQueryable<Package>
    {
        private SearchHijackerProvider _provider;
        public IQueryable<Package> Inner { get; private set; }

        public SearchHijacker(IQueryable<Package> inner, ISearchService service, string feed, string siteRoot, bool includeLicenseReport)
        {
            Inner = inner;
            _provider = new SearchHijackerProvider(inner.Provider, service as IRawSearchService, feed, siteRoot, includeLicenseReport);
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

        private class SearchHijackerProvider : IQueryProvider
        {
            private static MemberInfo _normalizedVersionMember = typeof(V2FeedPackage).GetProperty("NormalizedVersion");
            private static MemberInfo _versionMember = typeof(V2FeedPackage).GetProperty("Version");
            private static MemberInfo _idMember = typeof(V2FeedPackage).GetProperty("Id");

            public IQueryProvider Inner { get; private set; }
            public IRawSearchService SearchService { get; private set; }
            public string Feed { get; private set; }
            public string SiteRoot { get; private set; }
            public bool IncludeLicenseReport { get; private set; }

            public SearchHijackerProvider(IQueryProvider inner, IRawSearchService service, string feed, string siteRoot, bool includeLicenseReport)
            {
                Inner = inner;
                SearchService = service;
                Feed = feed;
                SiteRoot = siteRoot;
                IncludeLicenseReport = includeLicenseReport;
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return Inner.CreateQuery<TElement>(expression);
            }

            public IQueryable CreateQuery(Expression expression)
            {
                IQueryable result;
                if (!TryHijack(expression, out result))
                {
                    return Inner.CreateQuery(expression);
                }
                return result;
            }

            public TResult Execute<TResult>(Expression expression)
            {
                return Inner.Execute<TResult>(expression);
            }

            public object Execute(Expression expression)
            {
                return Inner.Execute(expression);
            }

            private bool TryHijack(Expression expression, out IQueryable result)
            {
                result = null;

                if (SearchService == null)
                {
                    return false;
                }

                // Unravel the comparisons into a list we can reason about
                IList<Tuple<Target, string>> comparisons = new List<Tuple<Target, string>>();
                Expression remnant = expression;
                MethodCallExpression where;
                while (IsQueryableWhere(where = remnant as MethodCallExpression))
                {
                    var comparison = ExtractComparison(where);
                    if (comparison == null)
                    {
                        break;
                    }
                    else
                    {
                        // We recognize this comparison, record it and keep iterating on the nested expression
                        comparisons.Add(comparison);
                        remnant = where.Arguments[0];
                    }
                }

                // What's left?
                if (IsSelectV2FeedPackage(remnant as MethodCallExpression))
                {
                    // We can hijack!
                    result = Hijack(comparisons);
                    return true;
                }

                return false;
            }

            private static bool IsSelectV2FeedPackage(MethodCallExpression expr)
            {
                // We expect:
                //  Queryable.Select(<nested expression>, p => new V2FeedPackage() ...)
                var isSelect = expr != null &&
                    expr.Method.DeclaringType == typeof(Queryable) &&
                    String.Equals(expr.Method.Name, "Select", StringComparison.Ordinal);
                if (isSelect)
                {
                    var arg = Unquote(expr.Arguments[1]);
                    if (arg.NodeType == ExpressionType.Lambda) // p => new V2FeedPackage ...
                    {
                        var lambda = arg as LambdaExpression;
                        if (lambda.ReturnType == typeof(V2FeedPackage))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private IQueryable Hijack(IList<Tuple<Target, string>> comparisons)
            {
                // Perform the search using the search service and just return the result.
                return SearchService.RawSearch(new SearchFilter(SearchFilter.ODataInterceptContext)
                {
                    SearchTerm = BuildQuery(comparisons),
                    IncludePrerelease = true,
                    IncludeAllVersions = true,
                    Take = Take(comparisons),
                    CuratedFeed = new CuratedFeed() { Name = Feed },
                    SortOrder = SortOrder.Relevance
                }).Result.Data.ToV2FeedPackageQuery(SiteRoot, IncludeLicenseReport);
            }

            private static int Take(IList<Tuple<Target, string>> c)
            {
                // Fixes https://github.com/NuGet/NuGetGallery/issues/2390
                // The idea is to limit the number of results to be "just one" in we search by Id and Version (as that's our key, it should only ever yield 1 result but we want a failsafe here).
                // When a different comparison is done, just take 40 results.
                return (c.Count == 2 && ((c[0].Item1 == Target.Id && c[1].Item1 == Target.Version) || (c[1].Item1 == Target.Id && c[0].Item1 == Target.Version))) ? 1 : 40;
            }

            private static string BuildQuery(IList<Tuple<Target, string>> comparisons)
            {
                StringBuilder query = new StringBuilder();
                bool first = true;
                foreach (var comparison in comparisons)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        query.Append(" AND ");
                    }
                    query.Append(comparison.Item1.ToString());
                    query.Append(":\"");
                    query.Append(comparison.Item2);
                    query.Append("\"");
                }
                return query.ToString();
            }

            private static Tuple<Target, string> ExtractComparison(MethodCallExpression outerWhere)
            {
                // We expect to see an expression that looks like this:
                //  Queryable.Where(<nested expression>, p => <constant> == p.<property>);
                var arg = Unquote(outerWhere.Arguments[1]);
                if (arg.NodeType != ExpressionType.Lambda)
                {
                    return null;
                }
                var lambda = arg as LambdaExpression; // p => <constant> == p.<property>
                if (lambda.Body.NodeType != ExpressionType.Equal) 
                {
                    return null;
                }
                var binExpr = lambda.Body as BinaryExpression; // <constant> == p.<property>

                // Get the two sides, we don't care which side is left and which is right.
                ConstantExpression constSide = (binExpr.Left as ConstantExpression) ?? (binExpr.Right as ConstantExpression);
                if (constSide == null || constSide.Type != typeof(string))
                {
                    return null;
                }
                MemberExpression memberSide = (binExpr.Right as MemberExpression) ?? (binExpr.Left as MemberExpression);
                if (memberSide == null)
                {
                    return null;
                }

                // Check if it's a known member comparison
                if (memberSide.Member == _normalizedVersionMember)
                {
                    return Tuple.Create(Target.Version, (string)constSide.Value);
                }
                else if (memberSide.Member == _versionMember)
                {
                    return Tuple.Create(Target.Version, SemanticVersionExtensions.Normalize((string)constSide.Value));
                }
                else if (memberSide.Member == _idMember)
                {
                    return Tuple.Create(Target.Id, (string)constSide.Value);
                }
                return null;
            }

            private static Expression Unquote(Expression expression)
            {
                if (expression.NodeType == ExpressionType.Quote)
                {
                    return ((UnaryExpression)expression).Operand;
                }
                return expression;
            }

            private static bool IsQueryableWhere(MethodCallExpression expr)
            {
                return expr != null &&
                    expr.Method.DeclaringType == typeof(Queryable) &&
                    String.Equals(expr.Method.Name, "Where", StringComparison.Ordinal);
            }

            private enum Target
            {
                Version,
                Id
            }
        }
    }

    public static class SearchHijackerExtensions
    {
        public static IQueryable<Package> UseSearchService(this IQueryable<Package> self, ISearchService searchService, string feed, string siteRoot, bool includeLicenseReport)
        {
            return new SearchHijacker(self, searchService, feed, siteRoot, includeLicenseReport);
        }
    }
}
