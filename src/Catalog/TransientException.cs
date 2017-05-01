// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Runtime.Serialization;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Thrown for intermittent issues where the service can continue without the process being shutdown. 
    /// </summary>
    [Serializable] 
    public class TransientException : System.Exception
    {
        public TransientException(string message ) : base(message)
        {
        }

        public TransientException(string message, Exception innerException) : base(message, innerException) 
        {
        }

        protected TransientException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    /// <summary>
    /// Thrown when an <see cref="HttpClient"/> times out.
    /// </summary>
    [Serializable]
    public class HttpClientTimeoutException : TransientException
    {
        public HttpClientTimeoutException(string message) : base(message)
        {
        }

        public HttpClientTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HttpClientTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
