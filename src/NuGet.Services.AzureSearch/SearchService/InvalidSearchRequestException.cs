// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// This exception is meant to be caught by an exception filter in the web application so that HTTP status code 400
    /// is returned to the user. The message in this exception is meant to become visible to the user.
    /// </summary>
    public class InvalidSearchRequestException : Exception
    {
        /// <summary>
        /// Create a new invalid search request exception.
        /// </summary>
        /// <param name="message">The message to display to the user. Must not contain sensitive information.</param>
        public InvalidSearchRequestException(string message)
            : base(message)
        {
        }
    }
}
