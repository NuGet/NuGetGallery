// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Http;
using System.Web.Http.Results;
using NuGetGallery.WebApi;

namespace NuGetGallery
{
    public static class HttpActionResultExtensions
    {
        public static T ExpectOkNegotiatedContentResult<T>(this IHttpActionResult actionResult)
        {
            var negotiatedContentResult = actionResult as OkNegotiatedContentResult<T>;
            if (negotiatedContentResult != null)
            {
                return negotiatedContentResult.Content;
            }
            throw new ArgumentException(string.Format("The argument is not of type OkNegotiatedContentResult<{0}>. Got {1} instead.", 
                typeof(T).FullName, actionResult.GetType().FullName), "actionResult");
        }

        public static QueryResult<T> ExpectQueryResult<T>(this IHttpActionResult actionResult)
        {
            var queryResult = actionResult as QueryResult<T>;
            if (queryResult != null)
            {
                return queryResult;
            }
            throw new ArgumentException(string.Format("The argument is not of type QueryResult<{0}>. Got {1} instead.",
                typeof(T).FullName, actionResult.GetType().FullName), "actionResult");
        }

        public static TExpectedResultType ExpectResult<TExpectedResultType>(this IHttpActionResult actionResult)
            where TExpectedResultType : IHttpActionResult
        {
            try
            {
                return (TExpectedResultType) actionResult;
            }
            catch
            {
                throw new ArgumentException(string.Format("The argument is not of type {0}. Got {1} instead.",
                    typeof (TExpectedResultType).FullName, actionResult.GetType().FullName), "actionResult");
            }
        }
    }
}
