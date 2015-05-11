// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery
{
    public class CountInterceptor : ExpressionVisitor
    {
        private readonly long _count;

        public CountInterceptor(long count)
        {
            _count = count;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;
            if ((method.DeclaringType == typeof(Queryable)) && method.Name.Equals("LongCount", StringComparison.Ordinal))
            {
                return Expression.Constant(_count);
            }

            return base.VisitMethodCall(node);
        }
    }
}