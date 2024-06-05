// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Http;

namespace NuGetGallery.WebApi
{
    public static class QueryResultExtensions
    {
        public static IHttpActionResult FormattedAsCountResult<T>(this IHttpActionResult current)
        {
            if (current is QueryResult<T> queryResult)
            {
                queryResult.FormatAsCountResult = true;
                return queryResult;
            }

            return current;
        }

        public static IHttpActionResult FormattedAsSingleResult<T>(this IHttpActionResult current)
        {
            if (current is QueryResult<T> queryResult)
            {
                queryResult.FormatAsSingleResult = true;
                return queryResult;
            }

            return current;
        }
    }
}