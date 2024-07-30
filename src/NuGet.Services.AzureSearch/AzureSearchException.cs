// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// An exception occurred while interacting with the Azure Search SDK. This exception is meant to be caught by an
    /// exception filter in the web application so that HTTP status code 503 is returned to the user. The message in
    /// this exception is not meant to become visible to the user.
    /// </summary>
    public class AzureSearchException : Exception
    {
        public AzureSearchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
