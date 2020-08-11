// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class TryGetResponseResult
    {
        public HttpResponseMessage HttpResponseMessage { get; set; }
        public AttemptResult AttemptResult;

        public static TryGetResponseResult Success(HttpResponseMessage httpResponseMessage)
        {
            return new TryGetResponseResult
            {
                AttemptResult = AttemptResult.Success,
                HttpResponseMessage = httpResponseMessage,
            };
        }

        public static TryGetResponseResult FailCanRetry()
        {
            return new TryGetResponseResult
            {
                AttemptResult = AttemptResult.FailCanRetry,
                HttpResponseMessage = null,
            };
        }

        public static TryGetResponseResult FailCannotRetry()
        {
            return new TryGetResponseResult
            {
                AttemptResult = AttemptResult.FailCannotRetry,
                HttpResponseMessage = null,
            };
        }
    }
}
