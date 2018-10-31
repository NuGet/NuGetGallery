// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Http.OData.Query;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Query;
using Microsoft.Data.OData.Query.SemanticAst;
using NuGet.Services.Entities;
using NuGetGallery.WebApi;

namespace NuGetGallery.OData
{
    public class HijackableQueryParameters
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }

    public class SearchHijacker
    {
        private static readonly MemberInfo NormalizedVersionMember = typeof(V2FeedPackage).GetProperty("NormalizedVersion");
        private static readonly MemberInfo VersionMember = typeof(V2FeedPackage).GetProperty("Version");
        private static readonly MemberInfo IdMember = typeof(V2FeedPackage).GetProperty("Id");

        private static readonly IQueryable<V2FeedPackage> EmptyQueryable = Enumerable.Empty<Package>().Select(p => new V2FeedPackage()).AsQueryable();

        public static bool IsHijackable(ODataQueryOptions<V2FeedPackage> options, out HijackableQueryParameters hijackable)
        {
            // Check if we can process the filter clause
            if (!CanProcessFilterClause(options))
            {
                hijackable = null;
                return false;
            }

            // Build expression (this works around all internal classes in the OData library - all we want is an expression tree)
            var expression = options.ApplyTo(EmptyQueryable, QueryResultDefaults.DefaultQuerySettings).Expression;

            // Unravel the comparisons into a list we can reason about
            List<Tuple<Target, string>> comparisons = new List<Tuple<Target, string>>();
            Expression remnant = FindQueryableWhere(expression as MethodCallExpression);
            MethodCallExpression where;
            while (IsQueryableWhere(where = remnant as MethodCallExpression))
            {
                var extractedComparisons = ExtractComparison(where).ToList();
                if (!extractedComparisons.Any() || extractedComparisons.Any(c => c == null))
                {
                    hijackable = null;
                    return false;
                }
                else
                {
                    // We recognize this comparison, record it and keep iterating on the nested expression
                    comparisons.AddRange(extractedComparisons);
                    remnant = where.Arguments[0];
                }
            }

            // We should be able to hijack here
            if (comparisons.Any())
            {
                hijackable = new HijackableQueryParameters();
                foreach (var comparison in comparisons)
                {
                    if (comparison.Item1 == Target.Id)
                    {
                        hijackable.Id = comparison.Item2;
                    }
                    else if (comparison.Item1 == Target.Version)
                    {
                        hijackable.Version = comparison.Item2;
                    }
                    else
                    {
                        hijackable = null;
                        return false;
                    }
                }

                return true;
            }

            hijackable = null;
            return false;
        }

        private static bool CanProcessFilterClause(ODataQueryOptions<V2FeedPackage> options)
        {
            // Check if we can read the filter clause
            try
            {
                var dummy = options.Filter?.FilterClause;
            }
            catch (ODataException)
            {
                // If that fails, we can't process the filter clause
                return false;
            }

            // If the filter clause can be read, it may not be a valid expression tree.
            // Example is '/api/v2/Packages?$filter=substringof(null,Id)' which throws ODataException:
            //     "The 'substringof' function cannot be applied to an enumeration-typed argument."
            // Skip hijacking queries that use a substringof filter due to the cost of validating PropertyAccess nodes
            if (options.Filter?.FilterClause != null
                && options.Filter.FilterClause.ItemType.Definition.TypeKind == EdmTypeKind.Entity)
            {
                var current = options.Filter.FilterClause.Expression;
                while (current.Kind == QueryNodeKind.BinaryOperator)
                {
                    var currentBinaryNode = ((BinaryOperatorNode)current);
                    if (IsSubstringOfFunctionCall(currentBinaryNode.Right))
                    {
                        return false;
                    }
                    current = currentBinaryNode.Left;
                }
                return !IsSubstringOfFunctionCall(current);
            }

            return true;
        }

        private static bool IsSubstringOfFunctionCall(SingleValueNode expression)
        {
            // If necessary, unwrap SingleValueFunctionCall from ConvertNode
            if (expression.Kind == QueryNodeKind.Convert)
            {
                expression = ((ConvertNode)expression).Source;
            }

            if (expression.Kind == QueryNodeKind.SingleValueFunctionCall)
            {
                var functionCallExpression = (SingleValueFunctionCallNode)expression;
                return string.Equals(functionCallExpression.Name, "substringof", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static IEnumerable<Tuple<Target, string>> ExtractComparison(MethodCallExpression outerWhere)
        {
            // We expect to see an expression that looks like this:
            //  Queryable.Where(<nested expression>, p => <constant> == p.<property>);
            var arg = Unquote(outerWhere.Arguments[1]);
            if (arg.NodeType != ExpressionType.Lambda)
            {
                yield break;
            }

            // p => <constant> == p.<property>
            // OR: p => <constant> == p.<property> && <constant> == p.<property>
            var lambda = arg as LambdaExpression;
            if (lambda.Body.NodeType == ExpressionType.AndAlso)
            {
                var binExpr = lambda.Body as BinaryExpression;

                var left = binExpr.Left as BinaryExpression;
                if (left != null)
                {
                    yield return ExtractComparison(left);
                }

                var right = binExpr.Right as BinaryExpression;
                if (right != null)
                {
                    yield return ExtractComparison(right);
                }
            }
            if (lambda.Body.NodeType == ExpressionType.Equal)
            {
                var binExpr = lambda.Body as BinaryExpression;
                yield return ExtractComparison(binExpr);
            }
        }

        private static Tuple<Target, string> ExtractComparison(BinaryExpression binExpr)
        {
            // p => <constant> == p.<property>
            if (binExpr.NodeType != ExpressionType.Equal)
            {
                return null;
            }

            // Get the two sides, we don't care which side is left and which is right.
            ConstantExpression constSide = (binExpr.Left as ConstantExpression) ?? (binExpr.Right as ConstantExpression);
            if (constSide == null || constSide.Type != typeof(string))
            {
                return null;
            }

            MemberExpression memberSide = (binExpr.Right as MemberExpression) ?? (binExpr.Left as MemberExpression);
            if (memberSide == null)
            {
                // That did not work... This may be Web API OData wrapping our expression
                UnaryExpression temp = binExpr.Left as UnaryExpression;
                if (temp != null)
                {
                    memberSide = temp.Operand as MemberExpression;
                }

                // Not found - for real.
                if (memberSide == null)
                {
                    return null;
                }
            }

            // Check if it's a known member comparison
            if (memberSide.Member == NormalizedVersionMember)
            {
                return Tuple.Create(Target.Version, (string)constSide.Value);
            }
            else if (memberSide.Member == VersionMember)
            {
                return Tuple.Create(Target.Version, NuGetVersionFormatter.Normalize((string)constSide.Value));
            }
            else if (memberSide.Member == IdMember)
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

        private static Expression FindQueryableWhere(MethodCallExpression expr)
        {
            if (expr != null && expr.Method.DeclaringType == typeof(Queryable))
            {
                if (!String.Equals(expr.Method.Name, "Where", StringComparison.Ordinal) && expr.Arguments.Any())
                {
                    var innerExpr = expr.Arguments[0] as MethodCallExpression;

                    expr = FindQueryableWhere(innerExpr) as MethodCallExpression;
                }
            }

            return expr;
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
