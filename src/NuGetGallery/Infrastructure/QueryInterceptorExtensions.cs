// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery.Infrastructure
{
    public static class QueryInterceptorExtensions
    {
        public static bool IsQueryTranslator<T>(this IQueryable<T> source)
        {
            return source.GetType().Name == "QueryTranslator`1";
        }
    }
}